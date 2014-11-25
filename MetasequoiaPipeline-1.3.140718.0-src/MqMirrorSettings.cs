#region ファイル説明
//-----------------------------------------------------------------------------
// MqMirrorSettings.cs
//=============================================================================
#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイアのミラーリング情報
    /// </summary>
    public class MqMirrorSettings
    {
        /// <summary>
        /// ミラーリング種類の取得と設定
        /// </summary>
        public MqMirrorType Type { get; set; }

        /// <summary>
        /// ミラーリング軸情報の取得と設定
        /// </summary>
        public MqMirrorAxies Axis { get; set; }

        /// <summary>
        /// ミラーリング面接続の制限距離の取得と設定
        /// </summary>
        public float? Distance { get; set; }

    }
}
