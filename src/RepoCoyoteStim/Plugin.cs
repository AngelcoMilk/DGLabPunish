using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RepoCoyoteStim
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "cn.codex.repo.coyotestim";
        public const string PluginName = "RepoCoyoteStim";
        public const string PluginVersion = "0.5.9";

        internal static Plugin Instance;
        internal static ManualLogSource Log;

        private Harmony _harmony;
        private DGLabSocketServer _server;
        private StimController _stim;
        private ControlPanel _panel;
        private bool _autoArmedForCurrentBind;

        internal DGLabSocketServer Server
        {
            get { return _server; }
        }

        internal StimController Stim
        {
            get { return _stim; }
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            ModConfig.Bind(Config);
            StimProfileManager.RefreshProfiles();

            _server = new DGLabSocketServer();
            _stim = new StimController(_server);
            _panel = new ControlPanel(_server, _stim);
            _autoArmedForCurrentBind = false;

            if (ModConfig.AutoStartServer.Value)
            {
                _server.Start();
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo("RepoCoyoteStim loaded.");
            Logger.LogInfo("DG-LAB QR URL: " + _server.GetQrUrl());
        }

        private void Update()
        {
            if (Input.GetKeyDown(ModConfig.PanelKey.Value))
            {
                _panel.ToggleVisible();
            }

            if (Input.GetKeyDown(ModConfig.EmergencyStopKey.Value))
            {
                _stim.EmergencyStop();
            }

            if (_server != null)
            {
                _server.Tick();
                if (_server.IsBound)
                {
                    if (ModConfig.AutoArmOnBind.Value && !_autoArmedForCurrentBind)
                    {
                        ModConfig.Armed.Value = true;
                        Config.Save();
                        _autoArmedForCurrentBind = true;
                        Logger.LogInfo("RepoCoyoteStim auto-armed after DG-LAB binding.");
                    }
                }
                else
                {
                    _autoArmedForCurrentBind = false;
                }
            }

            if (_stim != null)
            {
                _stim.Tick();
            }

            if (_panel != null)
            {
                _panel.UpdateCursor();
            }
        }

        private void OnGUI()
        {
            if (_panel != null)
            {
                _panel.Draw();
            }
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }

            if (_server != null)
            {
                _server.Stop();
            }

            if (_panel != null)
            {
                _panel.SetVisible(false);
            }
        }
    }
}
