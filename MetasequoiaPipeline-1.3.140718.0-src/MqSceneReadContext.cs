#region ファイル説明
//-----------------------------------------------------------------------------
// MqSceneReadContext.cs
//=============================================================================
#endregion

#region Using ステートメント

using System.Text;
using Microsoft.Xna.Framework.Content.Pipeline;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイアシーン読み込み用コンテキスト
    /// </summary>
    public class MqSceneReadContext
    {
        /// <summary>
        /// 読み込み中のファイル名の取得
        /// </summary>
        public string Filename { get; private set; }

        /// <summary>
        /// モデルファイルの文字エンコーディング
        /// </summary>
        public Encoding TextEncoding { get; set; }

        /// <summary>
        /// 不可視モデルのインポート設定
        /// </summary>
        public bool ImportInvisibleObjects { get; set; }

        /// <summary>
        /// メタセコイア形式データを保持するか？
        /// </summary>
        public bool PreserveMqScene { get; set; }

        /// <summary>
        /// インデックスバッファのサイズを16ビットに抑えるか？
        /// </summary>
        public bool UseSixteenBitsIndex { get; set; }

        /// <summary>
        /// インポーターコンテキストの取得
        /// </summary>
        public ContentImporterContext ImporterContext { get; private set; }

        public MqSceneReadContext(
            string filename,
            ContentImporterContext importerContext)
        {
            Filename = filename;
            TextEncoding = Encoding.GetEncoding("shift_jis");
            ImportInvisibleObjects = false;
            PreserveMqScene = true;
            UseSixteenBitsIndex = true;
            ImporterContext = importerContext;
        }

        #region インターナルフィールド

        // 読み込み中のシーンオブジェクト
        internal MqScene scene;

        #endregion
    }
}
