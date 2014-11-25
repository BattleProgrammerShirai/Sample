#region ファイル説明
//-----------------------------------------------------------------------------
// MqTokenizer.cs
//=============================================================================
#endregion

#region Using ステートメント

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// Mqoファイルのトーカナイザー
    /// </summary>
    /// <remarks>
    /// MQOファイルフォーマットの仕様を見ると、一行毎のパースをするようになっているが
    /// 実際の動作から察するに単純なトーカナイザを使用していると思われる。
    /// そこで、単純なトークン解析と、階層構造のチャンク読み込み機能をこのクラスが提供する。
    /// トークン区切りには空白とカッコ"()"を使用している
    /// </remarks>
    internal class MqoTokenizer : IDisposable
    {
        #region プロパティ

        /// <summary>
        /// ファイルの読み込み進行度(0-1)の取得
        /// </summary>
        public float Progress { get { return progress; } }

        /// <summary>
        /// 現在の読み込み行位置の取得
        /// </summary>
        public int LineNumber { get { return lineCount; } }

        #endregion

        /// <summary>
        /// 生成
        /// </summary>
        /// <param name="filename">ファイル名</param>
        /// <param name="encoding">文字エンコーディング</param>
        public MqoTokenizer(string filename, Encoding encoding)
        {
            textReader = new StreamReader(filename, encoding);
            lineCount = 0;
            progress = 0;
        }

        /// <summary>
        /// 次のトークンの取得
        /// </summary>
        /// <returns>トークン、ファイルの最後まで達した場合はnullを返す</returns>
        public string GetToken()
        {
            string token = String.Empty;

            if (tokens != null && tokenCount < tokens.Count)
            {
                token = tokens[tokenCount++];
            }
            else
            {
                if (!ProcessNewLine())
                    return null;

                tokenCount = 0;
                token = tokens[tokenCount++];
            }

            if (String.Compare(token, "{") == 0)
            {
                nest++;
            }
            else if (String.Compare(token, "}") == 0)
            {
                nest--;
            }

            return token;
        }

        #region ヘルパーメソッド

        /// <summary>
        /// 指定されたトークン文字(大文字小文字の区別なし)が続いていることを確認する
        /// </summary>
        public bool EnsureTokens(params string[] expectedTokens)
        {
            for (int i = 0; i < expectedTokens.Length; ++i)
            {
                string token = GetToken();

                if (String.Compare(token, expectedTokens[i], true) != 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 次に現れるチャンクを読み飛ばす
        /// </summary>
        public void SkipChunk()
        {
            while (GetToken() != "{")
                ;
            SkipTokens();
        }

        /// <summary>
        /// 現在のチャンク内のトークンを全て読み飛ばす
        /// </summary>
        public void SkipTokens()
        {
            int targetNest = nest - 1;
            do
            {
                GetToken();
            } while (nest != targetNest);
        }

        /// <summary>
        /// 次トークンをInt32値として取得する
        /// </summary>
        public int GetInt32()
        {
            string token = GetToken();
            return Int32.Parse(token);
        }

        /// <summary>
        /// 次トークンをHEX値(例:0x123456)として取得する
        /// </summary>
        public int GetHex32()
        {
            string token = GetToken();
            return (int)Int64.Parse(token, System.Globalization.NumberStyles.HexNumber);
        }

        /// <summary>
        /// 次トークンをSingle値として取得する
        /// </summary>
        public float GetSingle()
        {
            string token = GetToken();
            return Single.Parse(token);
        }

        /// <summary>
        /// 次トークンをColor値として取得する
        /// </summary>
        public Color GetColor()
        {
            string token = GetToken();
            int val = (int)Int64.Parse(token, System.Globalization.NumberStyles.Integer);
            return new Color(
                (int)(val & 0xff),
                (int)((val >> 8) & 0xff),
                (int)((val >> 16) & 0xff),
                (int)((val >> 24) & 0xff));
        }

        /// <summary>
        /// 次トークンをVector2値として取得する
        /// </summary>
        public Vector2 GetVector2()
        {
            float x = GetSingle();
            float y = GetSingle();
            return new Vector2(x, y);
        }

        /// <summary>
        /// 次トークンをVector3値として取得する
        /// </summary>
        public Vector3 GetVector3()
        {
            float x = GetSingle();
            float y = GetSingle();
            float z = GetSingle();
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// 次トークンをVector4値として取得する
        /// </summary>
        public Vector4 GetVector4()
        {
            float x = GetSingle();
            float y = GetSingle();
            float z = GetSingle();
            float w = GetSingle();
            return new Vector4(x, y, z, w);
        }

        /// <summary>
        /// 次トークンをString値として取得する
        /// </summary>
        public string GetString()
        {
            return GetToken().Trim('\"');
        }

        /// <summary>
        /// ファイルオブジェクトの破棄
        /// </summary>
        public void Dispose()
        {
            if (textReader != null)
            {
                textReader.Dispose();
                textReader = null;
            }
        }

        #endregion

        #region プライベートメソッド

        /// <summary>
        /// 次の行を読み込む
        /// </summary>
        /// <returns>ファイルの最後に到達したか？</returns>
        bool ProcessNewLine()
        {
            // 次の行の読み込み
            string line = String.Empty;
            do
            {
                line = textReader.ReadLine();
                lineCount++;
                if (line == null) return false;

                line.Trim();

                progress = (float)textReader.BaseStream.Position /
                            (float)textReader.BaseStream.Length;

            } while (String.IsNullOrEmpty(line));

            if (tokens == null)
                tokens = new List<string>();
            else
                tokens.Clear();

            //
            bool recordingString = false;   // '"'で囲まれた文字列をパース中か？
            bool recordingToken = false;    // トークンの記録中か？

            // 文字列格納先に十分な余裕があるか？
            if (token.Capacity < line.Length)
                token.Capacity = line.Length + (line.Length % 1024);
            token.Length = 0;

            // 現在の行をパースする
            foreach (char c in line)
            {
                // '"'で囲まれた文字列を取得する
                if (recordingString)
                {
                    token.Append(c);
                    if (c == '"')
                    {
                        tokens.Add(token.ToString());
                        token.Length = 0;
                        recordingString = recordingToken = false;
                    }
                }
                else if (Char.IsWhiteSpace(c) || c == '(' || c == ')')
                {
                    // トークン区切り文字だったか？
                    if (recordingToken)
                    {
                        tokens.Add(token.ToString());
                        token.Length = 0;
                        recordingToken = false;
                    }
                }
                else
                {
                    switch (c)
                    {
                        case '{':
                        case '}':
                            if (recordingToken)
                            {
                                tokens.Add(token.ToString());
                                token.Length = 0;
                                recordingToken = false;
                            }
                            tokens.Add(c.ToString());
                            break;
                        case '"':
                            token.Append(c);
                            recordingString = recordingToken = true;
                            break;
                        default:
                            token.Append(c);
                            recordingToken = true;
                            break;
                    }
                }
            }

            //
            if (recordingToken)
                tokens.Add(token.ToString());

            return true;
        }

        #endregion

        #region プライベートフィールド

        // 読み込みストリーム
        StreamReader textReader;

        // 行番号
        int lineCount;

        // ファイルの読み込み進行度
        float progress;

        // 現在処理中のトークン
        StringBuilder token = new StringBuilder(1024);

        // 現在行に含まれるトークンリスト
        List<string> tokens;

        // 現在行で読み込んだトークン数
        int tokenCount;

        // チャンクのネスト数
        int nest;

        #endregion

    }
}
