using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace RepoCoyoteStim
{
    internal sealed class DGLabSocketServer
    {
        private static readonly Regex StrengthRegex = new Regex("^strength-(\\d+)\\+(\\d+)\\+(\\d+)\\+(\\d+)$", RegexOptions.Compiled);
        private static readonly Regex SafeCodeRegex = new Regex("[^A-Za-z0-9_-]", RegexOptions.Compiled);

        private readonly object _sync = new object();
        private string _terminalId;
        private WebSocketServer _server;
        private WebSocket _remoteSocket;
        private AppSocketBehavior _session;
        private string _appId;
        private bool _bound;
        private bool _remoteConnected;
        private bool _remoteClientIdAssigned;
        private DateTime _lastHeartbeatUtc;
        private StrengthState _strength;
        private string _lastError;
        private string _lastRemoteEndpoint;
        private string _remoteStatus;

        internal DGLabSocketServer()
        {
            _terminalId = Guid.NewGuid().ToString();
            _appId = "";
            _strength = new StrengthState();
            _lastError = "";
            _lastRemoteEndpoint = "";
            _remoteStatus = "";
        }

        internal string ClientId
        {
            get { lock (_sync) return _terminalId; }
        }

        internal bool IsRunning
        {
            get
            {
                if (IsRemoteMode())
                {
                    lock (_sync) return _remoteSocket != null && _remoteConnected;
                }

                return _server != null && _server.IsListening;
            }
        }

        internal bool IsConnected
        {
            get
            {
                lock (_sync)
                {
                    if (IsRemoteMode())
                    {
                        return _remoteSocket != null && _remoteConnected;
                    }

                    return _session != null;
                }
            }
        }

        internal bool IsBound
        {
            get { lock (_sync) return _bound && (IsRemoteMode() ? _remoteConnected : _session != null); }
        }

        internal StrengthState Strength
        {
            get { lock (_sync) return _strength.Clone(); }
        }

        internal string LastError
        {
            get { lock (_sync) return _lastError; }
        }

        internal string LastRemoteEndpoint
        {
            get { lock (_sync) return _lastRemoteEndpoint; }
        }

        internal void Start()
        {
            if (IsRunning)
            {
                return;
            }

            if (IsRemoteMode())
            {
                StartRemoteClient();
            }
            else
            {
                StartLocalServer();
            }
        }

        private void StartLocalServer()
        {
            int port = Clamp(ModConfig.Port.Value, 1, 65535);
            try
            {
                _server = new WebSocketServer(IPAddress.Any, port);
                _server.AddWebSocketService<AppSocketBehavior>("/", delegate(AppSocketBehavior behavior)
                {
                    behavior.Owner = this;
                });
                _server.AddWebSocketService<AppSocketBehavior>("/" + _terminalId, delegate(AppSocketBehavior behavior)
                {
                    behavior.Owner = this;
                });
                _server.Start();
                _lastHeartbeatUtc = DateTime.UtcNow;
                lock (_sync)
                {
                    _lastError = "";
                    _remoteStatus = "";
                }
                Plugin.Log.LogInfo("DG-LAB local Socket server listening on port " + port + " with terminalId " + _terminalId);
            }
            catch (Exception ex)
            {
                _server = null;
                lock (_sync)
                {
                    _lastError = ex.Message;
                }
                Plugin.Log.LogError("Failed to start DG-LAB Socket server on port " + port + ": " + ex.Message);
            }
        }

        private void StartRemoteClient()
        {
            string uri = NormalizeBaseUri(ModConfig.RemoteServerUri.Value);
            if (string.IsNullOrEmpty(uri))
            {
                lock (_sync)
                {
                    _lastError = "远程 Socket 服务地址为空。";
                    _remoteStatus = _lastError;
                }
                return;
            }

            if (ModConfig.ConnectionMode.Value == SocketConnectionMode.RelayCode)
            {
                string code = SafeRelayCode(ModConfig.RemotePairCode.Value);
                if (!string.IsNullOrEmpty(code))
                {
                    lock (_sync)
                    {
                        _terminalId = code;
                    }
                }
            }

            try
            {
                _remoteSocket = new WebSocket(uri);
                _remoteSocket.OnOpen += delegate
                {
                    lock (_sync)
                    {
                        _remoteConnected = true;
                        _remoteClientIdAssigned = ModConfig.ConnectionMode.Value == SocketConnectionMode.RelayCode && !string.IsNullOrEmpty(SafeRelayCode(ModConfig.RemotePairCode.Value));
                        _bound = false;
                        _appId = "";
                        _lastError = "";
                        _remoteStatus = "已连接远程服务，等待终端 ID/绑定。";
                        _lastRemoteEndpoint = uri;
                    }
                    Plugin.Log.LogInfo("Connected to remote DG-LAB Socket service: " + uri);
                };
                _remoteSocket.OnMessage += delegate(object sender, MessageEventArgs e)
                {
                    HandleRemoteMessage(e.Data);
                };
                _remoteSocket.OnClose += delegate(object sender, CloseEventArgs e)
                {
                    lock (_sync)
                    {
                        _remoteConnected = false;
                        _remoteClientIdAssigned = false;
                        _bound = false;
                        _appId = "";
                        _strength = new StrengthState();
                        _remoteStatus = "远程服务已断开：" + e.Reason;
                    }
                    Plugin.Log.LogInfo("Remote DG-LAB Socket closed: " + e.Reason);
                };
                _remoteSocket.OnError += delegate(object sender, ErrorEventArgs e)
                {
                    lock (_sync)
                    {
                        _lastError = e.Message;
                        _remoteStatus = "远程服务错误：" + e.Message;
                    }
                    Plugin.Log.LogWarning("Remote DG-LAB Socket error: " + e.Message);
                };
                _remoteSocket.ConnectAsync();
            }
            catch (Exception ex)
            {
                _remoteSocket = null;
                lock (_sync)
                {
                    _lastError = ex.Message;
                    _remoteStatus = "远程服务启动失败：" + ex.Message;
                }
                Plugin.Log.LogError("Failed to connect remote DG-LAB Socket service: " + ex.Message);
            }
        }

        internal void Stop()
        {
            lock (_sync)
            {
                _bound = false;
                _session = null;
                _appId = "";
                _lastRemoteEndpoint = "";
                _remoteConnected = false;
                _remoteClientIdAssigned = false;
            }

            if (_remoteSocket != null)
            {
                try
                {
                    _remoteSocket.Close();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning("Failed to close remote DG-LAB Socket: " + ex.Message);
                }

                _remoteSocket = null;
            }

            if (_server != null)
            {
                try
                {
                    _server.Stop();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning("Failed to stop DG-LAB Socket server: " + ex.Message);
                }

                _server = null;
            }
        }

        internal void Tick()
        {
            if (!IsBound)
            {
                return;
            }

            int interval = Math.Max(5000, ModConfig.HeartbeatIntervalMs.Value);
            if ((DateTime.UtcNow - _lastHeartbeatUtc).TotalMilliseconds >= interval)
            {
                if (IsRemoteMode())
                {
                    SendRemoteRaw("heartbeat", "200");
                }
                else
                {
                    SendEnvelope("heartbeat", "200");
                }
                _lastHeartbeatUtc = DateTime.UtcNow;
            }
        }

        internal string GetQrUrl()
        {
            return "https://www.dungeon-lab.com/app-download.php#DGLAB-SOCKET#" + GetSocketBaseUri() + "/" + ClientId;
        }

        internal string GetSocketBaseUri()
        {
            SocketConnectionMode mode = ModConfig.ConnectionMode.Value;
            if (mode == SocketConnectionMode.RemoteServer || mode == SocketConnectionMode.RelayCode)
            {
                string remote = NormalizeBaseUri(ModConfig.RemoteServerUri.Value);
                return string.IsNullOrEmpty(remote) ? "ws://127.0.0.1:" + Clamp(ModConfig.Port.Value, 1, 65535) : remote;
            }

            string published = NormalizeBaseUri(ModConfig.PublishSocketUri.Value);
            if (mode == SocketConnectionMode.PublicSocket && !string.IsNullOrEmpty(published))
            {
                return published;
            }

            if (!string.IsNullOrEmpty(published))
            {
                return published;
            }

            return "ws://" + GetAdvertiseHost() + ":" + Clamp(ModConfig.Port.Value, 1, 65535);
        }

        internal string GetAdvertiseHost()
        {
            string configured = ModConfig.AdvertiseHost.Value;
            if (!string.IsNullOrEmpty(configured))
            {
                return configured.Trim();
            }

            string[] addresses = GetLocalIPv4Addresses();
            if (addresses.Length > 0)
            {
                return addresses[0];
            }

            return "127.0.0.1";
        }

        internal string[] GetLocalIPv4Addresses()
        {
            List<string> result = new List<string>();
            try
            {
                IPAddress[] addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
                for (int i = 0; i < addresses.Length; i++)
                {
                    IPAddress address = addresses[i];
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                    {
                        result.Add(address.ToString());
                    }
                }
            }
            catch
            {
            }

            return result.ToArray();
        }

        internal string LocalAddressSummary()
        {
            StringBuilder sb = new StringBuilder();
            string[] addresses = GetLocalIPv4Addresses();
            for (int i = 0; i < addresses.Length; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(addresses[i]);
            }
            return sb.Length == 0 ? "未检测到局域网 IPv4" : sb.ToString();
        }

        internal string StatusText()
        {
            if (IsRemoteMode())
            {
                string status;
                string error;
                lock (_sync)
                {
                    status = _remoteStatus;
                    error = _lastError;
                }

                if (IsBound)
                {
                    return "远程 Socket 已绑定";
                }

                if (IsConnected)
                {
                    return string.IsNullOrEmpty(status) ? "远程 Socket 已连接，等待绑定" : status;
                }

                return string.IsNullOrEmpty(error) ? "远程 Socket 未连接" : "远程 Socket 未连接：" + error;
            }

            if (!IsRunning)
            {
                string error = LastError;
                return string.IsNullOrEmpty(error) ? "服务未启动" : "服务未启动：" + error;
            }

            if (IsBound)
            {
                return "已绑定";
            }

            if (IsConnected)
            {
                return "已连接，等待绑定";
            }

            return "等待手机 App 连接";
        }

        internal string ConnectionModeText()
        {
            SocketConnectionMode mode = ModConfig.ConnectionMode.Value;
            if (mode == SocketConnectionMode.LocalServer) return "本地局域网";
            if (mode == SocketConnectionMode.PublicSocket) return "公网/隧道地址";
            if (mode == SocketConnectionMode.RemoteServer) return "远程 Socket 后端";
            return "自建 Relay Code";
        }

        internal void OnOpen(AppSocketBehavior behavior)
        {
            lock (_sync)
            {
                if (_session != null && !object.ReferenceEquals(_session, behavior))
                {
                    behavior.SendText(DgLabMessage.Create("error", "", "", "400").ToJson());
                    return;
                }

                _session = behavior;
                _bound = false;
                _appId = Guid.NewGuid().ToString();
                _strength = new StrengthState();
                _lastRemoteEndpoint = behavior.RemoteEndpoint;
            }

            behavior.SendText(DgLabMessage.Create("bind", _appId, "", "targetId").ToJson());
            Plugin.Log.LogInfo("DG-LAB app connected; assigned appId " + _appId + " from " + behavior.RemoteEndpoint);
        }

        internal void OnClose(AppSocketBehavior behavior)
        {
            lock (_sync)
            {
                if (object.ReferenceEquals(_session, behavior))
                {
                    _session = null;
                    _bound = false;
                    _appId = "";
                    _strength = new StrengthState();
                    _lastRemoteEndpoint = "";
                }
            }

            Plugin.Log.LogInfo("DG-LAB app disconnected.");
        }

        internal void OnMessage(AppSocketBehavior behavior, string raw)
        {
            DgLabMessage message;
            try
            {
                message = JsonConvert.DeserializeObject<DgLabMessage>(raw);
            }
            catch
            {
                behavior.SendText(DgLabMessage.Create("msg", "", "", "403").ToJson());
                return;
            }

            if (message == null || string.IsNullOrEmpty(message.Type))
            {
                behavior.SendText(DgLabMessage.Create("msg", "", "", "403").ToJson());
                return;
            }

            if (message.Type == "bind")
            {
                HandleBind(behavior, message);
                return;
            }

            if (message.Type == "msg")
            {
                HandleAppMessage(message);
                return;
            }
        }

        private void HandleBind(AppSocketBehavior behavior, DgLabMessage message)
        {
            bool ok;
            lock (_sync)
            {
                ok = object.ReferenceEquals(_session, behavior)
                    && message.ClientId == _terminalId
                    && message.TargetId == _appId;
                if (ok)
                {
                    _bound = true;
                }
            }

            if (ok)
            {
                behavior.SendText(DgLabMessage.Create("bind", _terminalId, _appId, "200").ToJson());
                Plugin.Log.LogInfo("DG-LAB app bound successfully.");
            }
            else
            {
                behavior.SendText(DgLabMessage.Create("bind", message.ClientId ?? "", message.TargetId ?? "", "400").ToJson());
            }
        }

        private void HandleRemoteMessage(string raw)
        {
            DgLabMessage message;
            try
            {
                message = JsonConvert.DeserializeObject<DgLabMessage>(raw);
            }
            catch
            {
                Plugin.Log.LogWarning("Remote DG-LAB Socket sent invalid JSON.");
                return;
            }

            if (message == null || string.IsNullOrEmpty(message.Type))
            {
                return;
            }

            if (message.Type == "bind")
            {
                HandleRemoteBind(message);
                return;
            }

            if (message.Type == "msg")
            {
                HandleAppMessage(message);
                return;
            }

            if (message.Type == "break")
            {
                lock (_sync)
                {
                    _bound = false;
                    _appId = "";
                    _remoteStatus = "远程配对已断开：" + message.Message;
                }
            }
        }

        private void HandleRemoteBind(DgLabMessage message)
        {
            lock (_sync)
            {
                if (message.Message == "targetId")
                {
                    if (ModConfig.ConnectionMode.Value != SocketConnectionMode.RelayCode || string.IsNullOrEmpty(SafeRelayCode(ModConfig.RemotePairCode.Value)))
                    {
                        if (!string.IsNullOrEmpty(message.ClientId))
                        {
                            _terminalId = message.ClientId;
                        }
                    }

                    _remoteClientIdAssigned = true;
                    _remoteStatus = "已获取远程终端 ID，等待手机扫码绑定。";
                    return;
                }

                if (message.Message == "200")
                {
                    _bound = true;
                    _appId = message.TargetId ?? "";
                    _lastHeartbeatUtc = DateTime.UtcNow;
                    _remoteStatus = "远程 Socket 已绑定。";
                    Plugin.Log.LogInfo("Remote DG-LAB app bound successfully.");
                    return;
                }

                _remoteStatus = "远程绑定失败：" + message.Message;
                _lastError = _remoteStatus;
            }
        }

        private void HandleAppMessage(DgLabMessage message)
        {
            Match match = StrengthRegex.Match(message.Message ?? "");
            if (!match.Success)
            {
                return;
            }

            lock (_sync)
            {
                _strength.ACurrent = ParseInt(match.Groups[1].Value);
                _strength.BCurrent = ParseInt(match.Groups[2].Value);
                _strength.AMax = ParseInt(match.Groups[3].Value);
                _strength.BMax = ParseInt(match.Groups[4].Value);
            }
        }

        internal bool SendPulse(char channel, IList<string> pulses)
        {
            if (pulses == null || pulses.Count == 0)
            {
                return false;
            }

            int maxItems = Clamp(ModConfig.MaxQueuedPulseItems.Value, 1, 100);
            int count = Math.Min(maxItems, pulses.Count);
            string payload = "pulse-" + channel + ":" + WaveformEncoder.ToJsonArray(pulses, count);
            return SendEnvelope("msg", payload);
        }

        internal bool ClearChannel(int channel)
        {
            return SendEnvelope("msg", "clear-" + channel);
        }

        internal void ClearAll()
        {
            ClearChannel(1);
            ClearChannel(2);
        }

        internal bool SetStrength(int channel, int value)
        {
            int strength = Clamp(value, 0, 200);
            return SendEnvelope("msg", "strength-" + channel + "+2+" + strength);
        }

        private bool SendEnvelope(string type, string command)
        {
            if (IsRemoteMode())
            {
                if (type == "msg")
                {
                    return SendRemoteDirect(command);
                }

                return SendRemoteRaw(type, command);
            }

            AppSocketBehavior session;
            string appId;

            lock (_sync)
            {
                if (_session == null || !_bound)
                {
                    return false;
                }

                session = _session;
                appId = _appId;
            }

            if (command != null && command.Length > 1950)
            {
                Plugin.Log.LogWarning("DG-LAB command was too long and has been dropped.");
                return false;
            }

            try
            {
                session.SendText(DgLabMessage.Create(type, _terminalId, appId, command).ToJson());
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Failed to send DG-LAB command: " + ex.Message);
                return false;
            }
        }

        private bool SendRemoteDirect(string command)
        {
            if (command != null && command.Length > 1950)
            {
                Plugin.Log.LogWarning("DG-LAB command was too long and has been dropped.");
                return false;
            }

            string terminalId;
            string appId;
            WebSocket socket;
            lock (_sync)
            {
                if (_remoteSocket == null || !_remoteConnected || !_bound)
                {
                    return false;
                }

                socket = _remoteSocket;
                terminalId = _terminalId;
                appId = _appId;
            }

            try
            {
                string json = JsonConvert.SerializeObject(new
                {
                    type = 4,
                    message = command,
                    clientId = terminalId,
                    targetId = appId
                });
                socket.Send(json);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Failed to send remote DG-LAB command: " + ex.Message);
                return false;
            }
        }

        private bool SendRemoteRaw(string type, string command)
        {
            string terminalId;
            string appId;
            WebSocket socket;
            lock (_sync)
            {
                if (_remoteSocket == null || !_remoteConnected)
                {
                    return false;
                }

                socket = _remoteSocket;
                terminalId = _terminalId;
                appId = _appId;
            }

            try
            {
                socket.Send(DgLabMessage.Create(type, terminalId, appId, command).ToJson());
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Failed to send remote DG-LAB raw message: " + ex.Message);
                return false;
            }
        }

        private bool IsRemoteMode()
        {
            SocketConnectionMode mode = ModConfig.ConnectionMode.Value;
            return mode == SocketConnectionMode.RemoteServer || mode == SocketConnectionMode.RelayCode;
        }

        private static string NormalizeBaseUri(string value)
        {
            string text = (value ?? "").Trim();
            while (text.EndsWith("/", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 1);
            }
            return text;
        }

        private static string SafeRelayCode(string value)
        {
            string text = (value ?? "").Trim();
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            return SafeCodeRegex.Replace(text, "");
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : 0;
        }

        internal sealed class AppSocketBehavior : WebSocketBehavior
        {
            public DGLabSocketServer Owner { get; set; }

            public string RemoteEndpoint
            {
                get
                {
                    try
                    {
                        return Context != null && Context.UserEndPoint != null ? Context.UserEndPoint.ToString() : "";
                    }
                    catch
                    {
                        return "";
                    }
                }
            }

            protected override void OnOpen()
            {
                Owner.OnOpen(this);
            }

            protected override void OnMessage(MessageEventArgs e)
            {
                Owner.OnMessage(this, e.Data);
            }

            protected override void OnClose(CloseEventArgs e)
            {
                Owner.OnClose(this);
            }

            public void SendText(string text)
            {
                Send(text);
            }
        }
    }
}
