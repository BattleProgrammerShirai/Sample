#region ファイル説明
//-----------------------------------------------------------------------------
// XmlListReader.cs
//=============================================================================
#endregion

#region Using ステートメント

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Intermediate;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// XMLファイルからリスト形式のデータを読む為のヘルパークラス
    /// </summary>
    /// <remarks>
    /// IntermediateReaderを使った場合、リストデータをシリアライズするとXMLエレメントが
    /// 以下の様に要素数分だけ作られるのでファイルサイズが大きくなってしまう。
    /// 
    /// [Item]1[/Item]
    /// [Item]2[/Item]
    ///       .
    ///       .
    ///       .
    /// [Item]n[/Item]
    /// 
    /// 特にモデルデータを格納する場合は無駄が多くなのでこのヘルパークラスでは以下の様な
    /// データをリストとして読めるようになっている
    /// 
    /// [Item]1 2 ... n[/Item]
    /// 
    /// </remarks>
    class XmlListReader
    {
        /// <summary>
        /// リストの最後か？
        /// </summary>
        public bool AtEnd { get { return items.Length <= itemIdx; } }

        /// <summary>
        /// リストデータの読み込み開始
        /// </summary>
        public XmlListReader(IntermediateReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            string text = reader.Xml.ReadContentAsString();

            items = text.Split(listSeparators, StringSplitOptions.RemoveEmptyEntries);
            itemIdx = 0;
        }

        /// <summary>
        /// 次の要素を覗く
        /// </summary>
        public string PeekString()
        {
            return items[itemIdx];
        }

        /// <summary>
        /// 次の要素を文字列として取得する
        /// </summary>
        public string ReadAsString()
        {
            return items[itemIdx++];
        }

        /// <summary>
        /// 次の要素をInt32として取得する
        /// </summary>
        public int ReadAsInt32()
        {
            return Int32.Parse(ReadAsString());
        }

        /// <summary>
        /// 次の要素をColor値として取得する
        /// </summary>
        public Color ReadAsColor()
        {
            string txt = ReadAsString();
            int val = (int)Int64.Parse(txt, System.Globalization.NumberStyles.HexNumber);
            return new Color(
                (int)(val & 0xff),
                (int)((val >> 8) & 0xff),
                (int)((val >> 16) & 0xff),
                (int)((val >> 24) & 0xff));
        }

        /// <summary>
        /// 次の要素をfloat値として取得する
        /// </summary>
        public float ReadAsSingle()
        {
            return Single.Parse(ReadAsString());
        }

        /// <summary>
        /// 次の要素をVector2値として取得する
        /// </summary>
        public Vector2 ReadAsVector2()
        {
            float x = Single.Parse(ReadAsString());
            float y = Single.Parse(ReadAsString());
            return new Vector2(x, y);
        }

        /// <summary>
        /// 次の要素をVector3値として取得する
        /// </summary>
        public Vector3 ReadAsVector3()
        {
            float x = Single.Parse(ReadAsString());
            float y = Single.Parse(ReadAsString());
            float z = Single.Parse(ReadAsString());
            return new Vector3(x, y, z);
        }

        #region プライベートフィールド

        // リスト
        string[] items;

        // 読み込んだ要素数
        int itemIdx;

        // 要素の区切り文字
        static char[] listSeparators =
        {
            ' ',        // 半角スペース
            '\u3000',   // 全角スペース
            '\n', '\r', '\t',　// タブ、改行など
        };

        #endregion

    }
}
