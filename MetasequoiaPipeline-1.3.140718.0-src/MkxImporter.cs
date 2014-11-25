#region ファイル説明
//-----------------------------------------------------------------------------
// MkxImporter.cs
//=============================================================================
#endregion

#region Using ステートメント

using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// XML形式メタセコイアモデルファイル(.Mkx)インポーター
    /// </summary>
    [ContentImporter(".mkx", DisplayName = "MKX形式メタセコイア モデルインポーター",
        DefaultProcessor = "MqModelProcessor")]
    public class MkxImporter : ContentImporter<NodeContent>
    {
        /// <summary>
        /// XML形式メタセコイアファイルのインポート
        /// </summary>
        /// <param name="filename">インポートするファイル名</param>
        /// <returns>インポート後のNodeContent</returns>
        public override NodeContent Import(string filename,
                                            ContentImporterContext context)
        {
            // MqSceneオブジェクトへの読み込みとNodeContentへの変換
            MqSceneReadContext readContext = new MqSceneReadContext(filename, context);
            MqScene scene = MqScene.CraeteFromFile(filename, readContext);

            return scene.Root;
        }

    }

}
