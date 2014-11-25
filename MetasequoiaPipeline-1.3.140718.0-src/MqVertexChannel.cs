#region ファイル説明
//-----------------------------------------------------------------------------
// MqVertexChannel.cs
//=============================================================================
#endregion

#region Using ステートメント

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイアの頂点チャンネル情報を格納する構造体
    /// </summary>
    public struct MqVertexChannel
    {
        /// <summary>
        /// テクスチャ座標
        /// </summary>
        public Vector2 Texcoord;

        /// <summary>
        /// 頂点カラー
        /// </summary>
        public Color Color;

        /// <summary>
        /// ボーンウェイト
        /// </summary>
        public BoneWeightCollection BoneWeights;
    }
}
