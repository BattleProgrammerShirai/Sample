#region ファイル説明
//-----------------------------------------------------------------------------
// MqVertex.cs
//=============================================================================
#endregion

#region Using ステートメント

using System.Collections.Generic;
using Microsoft.Xna.Framework;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイアの頂点情報を格納するクラス
    /// <summary>
    public sealed class MqVertex
    {
        #region プロパティ

        /// 頂点リスト内(MqMesh.Vertices)でのインデックスの取得
        /// </summary>
        public int Index { get; private set; }

        /// <summary>
        /// 頂点位置の取得
        /// </summary>
        public Vector3 Position { get; internal set; }

        /// <summary>
        /// この頂点を使用している面リストの取得
        /// </summary>
        public IList<MqFace> Faces { get { return _faces; } }

        List<MqFace> _faces;

        /// <summary>
        /// この頂点を使用している辺リストの取得
        /// </summary>
        public IList<MqEdge> Edges { get { return _edges; } }

        List<MqEdge> _edges;

        /// <summary>
        /// 次サブディビジョンレベルの頂点、Catmull-Clark処理時に使用する
        /// </summary>
        public MqVertex SubdividedVertex;

        #endregion

        #region インターナルメソッド

        /// <summary>
        /// 生成
        /// </summary>
        internal MqVertex(int index, Vector3 position)
        {
            Index = index;
            Position = position;
            _faces = new List<MqFace>();
        }

        /// <summary>
        /// 辺情報を格納するリストを確保する
        /// </summary>
        internal void AllocateEdges()
        {
            _edges = new List<MqEdge>();
        }

        #endregion

    }
}
