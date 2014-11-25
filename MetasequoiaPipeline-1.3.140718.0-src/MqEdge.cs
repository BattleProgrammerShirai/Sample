#region ファイル説明
//-----------------------------------------------------------------------------
// MqEdge.cs
//=============================================================================
#endregion

#region Using ステートメント

using System.Collections.Generic;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイアの辺情報を格納するクラス
    /// Catmull-Clarkやミラーリング処理時に使用される
    /// </summary>
    public sealed class MqEdge
    {
        #region プロパティ

        /// <summary>
        /// 辺を構成する頂点0の取得
        /// </summary>
        public MqVertex Vertex0 { get; private set; }

        /// <summary>
        /// 辺を構成する頂点1の取得
        /// </summary>
        public MqVertex Vertex1 { get; private set; }

        /// <summary>
        /// 次サブディビジョンレベルの頂点、Catmull-Clark処理時に使用する
        /// </summary>
        public MqVertex SubdividedVertex { get; set; }

        /// <summary>
        /// この辺を使っている面リストの取得
        /// </summary>
        public List<MqFace> Faces { get { return _faces; } }

        List<MqFace> _faces;

        #endregion

        /// <summary>
        /// 辺の生成
        /// </summary>
        public MqEdge(MqVertex vertex0, MqVertex vertex1)
        {
            Vertex0 = vertex0;
            Vertex1 = vertex1;
            _faces = new List<MqFace>();
        }

        /// <summary>
        /// 指定された頂点と反対側の頂点を取得する。
        /// </summary>
        public MqVertex GetOtherSide(MqVertex cur)
        {
            return cur == Vertex0 ? Vertex1 : Vertex0;
        }
    }

}
