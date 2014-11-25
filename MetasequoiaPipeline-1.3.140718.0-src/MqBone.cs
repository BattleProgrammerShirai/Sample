#region ファイル説明
//-----------------------------------------------------------------------------
// MqBone.cs
//=============================================================================
#endregion

#region Using ステートメント

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// ボーン情報
    /// Mkxファイル内に格納される
    /// </summary>
    public class MqBone
    {
        /// <summary>
        /// ボーン名の取得と設定
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 親ボーン名の取得と設定
        /// </summary>
        [ContentSerializer(Optional = true)]
        public string Parent { get; set; }

        /// <summary>
        /// ボーン行列の取得と設定
        /// </summary>
        public Matrix Transform { get; set; }
    }

}
