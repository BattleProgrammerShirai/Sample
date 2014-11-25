#region ファイル説明
//-----------------------------------------------------------------------------
// MqImporter.cs
//=============================================================================
#endregion

#region Using ステートメント

using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using System.IO;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイアモデルファイル(.Mqo)インポーター
    /// </summary>
    [ContentImporter(".mqo", DisplayName = "メタセコイア モデルインポーター",
        DefaultProcessor = "MqModelProcessor")]
    public class MqImporter : ContentImporter<NodeContent>
    {
        /// <summary>
        /// メタセコイアファイルのインポート
        /// </summary>
        /// <param name="filename">インポートするファイル名</param>
        /// <returns>インポート後のNodeContent</returns>
        public override NodeContent Import(string filename,
                                            ContentImporterContext context)
        {
            // MqSceneオブジェクトへの読み込みとNodeContentへの変換
            MqSceneReadContext settings = new MqSceneReadContext(filename, context);
            MqScene scene = MqScene.CraeteFromFile(filename, settings);

            scene.Root.Identity = new ContentIdentity(Path.GetFullPath(filename), "MqoImporter");
            return scene.Root;
        }

    }

}
