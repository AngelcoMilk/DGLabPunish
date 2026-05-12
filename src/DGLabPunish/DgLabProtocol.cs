using Newtonsoft.Json;

namespace DGLabPunish
{
    internal sealed class DgLabMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("clientId")]
        public string ClientId { get; set; }

        [JsonProperty("targetId")]
        public string TargetId { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        public static DgLabMessage Create(string type, string clientId, string targetId, string message)
        {
            return new DgLabMessage
            {
                Type = type,
                ClientId = clientId,
                TargetId = targetId,
                Message = message
            };
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    internal sealed class StrengthState
    {
        public int ACurrent;
        public int BCurrent;
        public int AMax;
        public int BMax;

        public StrengthState Clone()
        {
            return new StrengthState
            {
                ACurrent = ACurrent,
                BCurrent = BCurrent,
                AMax = AMax,
                BMax = BMax
            };
        }
    }
}
