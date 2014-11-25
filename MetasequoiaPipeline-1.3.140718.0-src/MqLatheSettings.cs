#region ファイル説明
//-----------------------------------------------------------------------------
// MqLatheSettings.cs
//=============================================================================
#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイアの回転体情報
    /// </summary>
    public class MqLatheSettings
    {
        /// <summary>
        /// 回転体タイプの取得と設定
        /// </summary>
        public MqLatheType Type { get; set; }

        /// <summary>
        /// 回転体の回転軸の取得と設定
        /// </summary>
        public MqLatheAxis Axis { get; set; }

        /// <summary>
        /// 回転体のセグメント数の取得と設定
        /// </summary>
        public int NumSegments { get; set; }

        /// <summary>
        /// 回転体情報の生成
        /// </summary>
        public MqLatheSettings()
        {
            Type = MqLatheType.None;
            Axis = MqLatheAxis.Y;
            NumSegments = 12;
        }

    }
}
