#region ファイル説明
//-----------------------------------------------------------------------------
// MqReader.cs
//=============================================================================
#endregion

#region Using ステートメント

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイア用モデルファイル(Mqo)読み込み用クラス
    /// </summary>
    public class MqReader : IDisposable
    {
        /// <summary>
        /// メタセコイアモデルファイルの読み込み
        /// </summary>
        public static MqScene ReadFromFile(
            string filename, MqSceneReadContext settings)
        {
            using (MqReader reader = new MqReader(filename, settings))
            {
                return reader.Read();
            }
        }

        /// <summary>
        /// 読み込み用オブジェクトの生成
        /// </summary>
        internal MqReader(string filename, MqSceneReadContext context)
        {
            mqoDirectory = Path.GetDirectoryName(Path.GetFullPath(filename));
            names = new Stack<string>();
            names.Push(null);

            this.context = context;
            tokenizer = new MqoTokenizer(filename, context.TextEncoding);

            context.scene = new MqScene
            {
                Root = new NodeContent { Name = MqScene.RootNodeName }
            };
        }

        /// <summary>
        /// 破棄
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// IDisposableインターフェースの実装
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                isDisposed = true;
                if (tokenizer != null)
                {
                    tokenizer.Dispose();
                    tokenizer = null;
                }
            }
        }


        /// <summary>
        /// ファイルの読み込み
        /// </summary>
        /// <returns></returns>
        MqScene Read()
        {
            // このファイルはメタセコイアファイルか？
            if (!tokenizer.EnsureTokens("Metasequoia", "Document",
                "Format", "Text", "Ver"))
            {
                throw new InvalidOperationException(
                    String.Format(CultureInfo.CurrentCulture,
                    Resources.InvalidFileFormat, context.Filename));
            }

            // このバージョンは対応しているか？
            var version = tokenizer.GetToken();
            if (version != "1.0" && version != "1.1")
            {
                throw new InvalidOperationException(
                    String.Format(CultureInfo.CurrentCulture,
                    Resources.NotSupportedVersionFile, context.Filename, version));
            }

            // 各チャンクの読み込み
            string token = tokenizer.GetToken();
            for (; token != null; token = tokenizer.GetToken())
            {
                string a = token.ToLower();
                switch (a)
                {
                    case "scene": ReadSceneChunk(); break;
                    case "trialnoise": ReadTrialNoiseChunk(); break;
                    case "thumbnail": ReadThumbnailChunk(); break;
                    case "includexml": ReadIncludeXmlChunk(); break;
                    case "backimage": ReadBackImageChunk(); break;
                    case "material": ReadMaterialChunk(); break;
                    case "object": ReadObjectChunk(); break;
                    case "eof": break;
                    default:
                        throw new InvalidOperationException(String.Format(
                            CultureInfo.CurrentCulture, Resources.UnknownChunk,
                            tokenizer.LineNumber, token));
                }
            }

            return context.scene;
        }

        #region 各チャンクの読み込み

        /// <summary>
        /// マテリアルチャンクの読み込み
        /// </summary>
        private void ReadMaterialChunk()
        {
            int numMaterials = tokenizer.GetInt32();
            tokenizer.EnsureTokens("{");

            BasicMaterialContent material = null;
            Vector4 color = new Vector4(0.8f, 0.8f, 0.8f, 1);
            float dif = 0.8f;
            float emi = 0;
            float spc = 0;
            float power = 5;

            bool done = false;
            while (!done)
            {
                string token = tokenizer.GetToken();

                // このトークンはマテリアル名か？
                if (token.StartsWith("\""))
                {
                    if (material != null)
                    {
                        AddMaterial(material, color, dif, emi, spc, power);

                        // 各パラメーターをデフォルト値に戻す
                        color = new Vector4(0.8f, 0.8f, 0.8f, 1);
                        dif = 0.8f; emi = spc = 0; power = 5;
                    }

                    material = new BasicMaterialContent();
                    material.Name = token.Trim('"');

                    continue;
                }

                switch (token.ToLower())
                {
                    case "shader":
                        /* 無視 */
                        tokenizer.GetInt32();
                        break;
                    case "col": color = tokenizer.GetVector4(); break;
                    case "vcol":
                        material.VertexColorEnabled = tokenizer.GetInt32() == 1; break;
                    case "dif": dif = tokenizer.GetSingle(); break;
                    case "amb": /* 無視 */ tokenizer.GetSingle(); break;
                    case "emi": emi = tokenizer.GetSingle(); break;
                    case "spc": spc = tokenizer.GetSingle(); break;
                    case "power": power = tokenizer.GetSingle(); break;
                    case "tex":
                        material.Texture = CreateTextureReference(tokenizer.GetString());
                        break;
                    case "aplane":
                        material.Textures.Add("AlphaTexture",
                            CreateTextureReference(tokenizer.GetString()));
                        break;
                    case "bump":
                        material.Textures.Add("BumpTexture",
                            new ExternalReference<TextureContent>(tokenizer.GetString()));
                        break;
                    case "proj_type": /* 無視 */ tokenizer.GetToken(); break;
                    case "proj_pos": /* 無視 */ tokenizer.GetVector3(); break;
                    case "proj_scale": /* 無視 */ tokenizer.GetVector3(); break;
                    case "proj_angle": /* 無視 */ tokenizer.GetVector3(); break;
                    case "}": done = true; break;
                    default:
                        throw new InvalidOperationException(String.Format(
                            CultureInfo.CurrentCulture, Resources.UnknownToken,
                            tokenizer.LineNumber, token));
                }
            }

            // 最後に読み込んだマテリアル情報の追加
            AddMaterial(material, color, dif, emi, spc, power);

        }

        /// <summary>
        /// バックイメージチャンクの読み込み
        /// </summary>
        private void ReadBackImageChunk()
        {
            tokenizer.SkipChunk();  // 無視
        }

        /// <summary>
        /// サムネイルチャンクの読み込み
        /// </summary>
        private void ReadThumbnailChunk()
        {
            tokenizer.SkipChunk();  // 無視
        }

        /// <summary>
        /// Xmlインクルードチャンクの読み込み
        /// </summary>
        private void ReadIncludeXmlChunk()
        {
            tokenizer.SkipChunk();  // 無視
        }

        /// <summary>
        /// 試用チャンクの読み込み
        /// </summary>
        private void ReadTrialNoiseChunk()
        {
            throw new InvalidOperationException(String.Format(
                CultureInfo.CurrentCulture, Resources.TrialChunkDetected,
                tokenizer.LineNumber));
        }

        /// <summary>
        /// シーンチャンクの読み込み
        /// </summary>
        private void ReadSceneChunk()
        {
            tokenizer.EnsureTokens("{");

            bool done = false;
            while (!done)
            {
                string token = tokenizer.GetToken();
                switch (token.ToLower())
                {
                    case "pos": /* 無視 */ tokenizer.GetVector3(); break;
                    case "lookat": /* 無視 */ tokenizer.GetVector3(); break;
                    case "head": /* 無視 */ tokenizer.GetSingle(); break;
                    case "pich": /* 無視 */ tokenizer.GetSingle(); break;
                    case "bank": /* 無視 */ tokenizer.GetSingle(); break;
                    case "ortho": /* 無視 */ tokenizer.GetSingle(); break;
                    case "zoom2": /* 無視 */ tokenizer.GetSingle(); break;
                    case "amb": /* 無視 */ tokenizer.GetVector3(); break;
                    case "dirlights": /* 無視 */ tokenizer.SkipChunk(); break;
                    case "}": done = true; break;
                    default:
                        throw new InvalidOperationException(String.Format(
                            CultureInfo.CurrentCulture, Resources.UnknownToken,
                            tokenizer.LineNumber, token));
                }
            }
        }

        #region Objectチャンクの読み込み

        /// <summary>
        /// オブジェクトチャンクの読み込み
        /// </summary>
        private void ReadObjectChunk()
        {
            string name = tokenizer.GetString();
            tokenizer.EnsureTokens("{");

            MqObject mqObj = new MqObject(context.scene, name);

            bool done = false;
            while (!done)
            {
                string token = tokenizer.GetToken();
                switch (token.ToLower())
                {
                    case "color": /* 無視 */ tokenizer.GetVector3(); break;
                    case "color_type": /* 無視 */ tokenizer.GetInt32(); break;
                    case "blob": /* 無視 */ tokenizer.SkipChunk(); break;
                    case "bvertex": /* 無視 */ tokenizer.SkipChunk(); break;
                    case "depth":
                        int nest = tokenizer.GetInt32() + 1;
                        while (nest < names.Count)
                            names.Pop();

                        mqObj.Parent = names.Peek();

                        break;
                    case "face": mqObj.Mesh = ReadFaces(mqObj); break;
                    case "facet":
                        mqObj.SmoothAngle = MathHelper.ToRadians(tokenizer.GetSingle());
                        break;
                    case "folding": /* 無視 */ tokenizer.GetToken(); break;
                    case "lathe":
                        mqObj.EnsureLatheSettings();
                        mqObj.LatheSettings.Type = (MqLatheType)tokenizer.GetInt32();
                        break;
                    case "lathe_axis":
                        mqObj.EnsureLatheSettings();
                        mqObj.LatheSettings.Axis = (MqLatheAxis)tokenizer.GetInt32();
                        break;
                    case "lathe_seg":
                        mqObj.EnsureLatheSettings();
                        mqObj.LatheSettings.NumSegments = tokenizer.GetInt32();
                        break;
                    case "locking": /* 無視 */ tokenizer.GetToken(); break;
                    case "mirror":
                        mqObj.EnsureMirrorSettings();
                        mqObj.MirrorSettings.Type = (MqMirrorType)tokenizer.GetInt32();
                        break;
                    case "mirror_axis":
                        mqObj.EnsureMirrorSettings();
                        mqObj.MirrorSettings.Axis = (MqMirrorAxies)tokenizer.GetInt32();
                        break;
                    case "mirror_dis":
                        mqObj.EnsureMirrorSettings();
                        mqObj.MirrorSettings.Distance = tokenizer.GetSingle();
                        break;
                    case "patch":
                        mqObj.PatchType = (MqPatchType)tokenizer.GetInt32();
                        if (mqObj.PatchType != MqPatchType.Polygon &&
                            mqObj.PatchType != MqPatchType.CatmullClark)
                        {
                            // 知らないパッチ形式を読み飛ばす
                            tokenizer.SkipTokens();
                            done = true;
                        }
                        break;
                    case "patchtri": /* 無視 */ tokenizer.GetToken(); break;
                    case "rotation":
                        mqObj.Rotation = tokenizer.GetVector3();
                        break;
                    case "scale":
                        mqObj.Scale = tokenizer.GetVector3();
                        break;
                    case "segment":
                        mqObj.PatchSegments = tokenizer.GetInt32();
                        break;
                    case "shading":
                        if (tokenizer.GetInt32() == 0)
                            mqObj.SmoothAngle = null;
                        break;
                    case "translation":
                        mqObj.Translation = tokenizer.GetVector3()
                            ; break;
                    case "vertex": ReadVertices(mqObj); break;
                    case "visible":
                        mqObj.IsVisible = tokenizer.GetInt32() != 0;
                        if (!mqObj.IsVisible && !context.ImportInvisibleObjects)
                        {
                            tokenizer.SkipTokens();
                            done = true;
                        }
                        break;
                    case "}": done = true; break;
                    default:
                        throw new InvalidOperationException(String.Format(
                            CultureInfo.CurrentCulture, Resources.UnknownToken,
                            tokenizer.LineNumber, token));
                }
            }

            // NodeContentの生成
            if (mqObj.IsVisible || context.ImportInvisibleObjects)
            {
                names.Push(mqObj.Name);
                context.scene.Objects.Add(mqObj);
            }
        }

        /// <summary>
        /// 頂点サブチャンクの読み込み
        /// </summary>
        void ReadVertices(MqObject mqObj)
        {
            if (mqObj.Mesh == null)
                mqObj.Mesh = new MqMesh();

            MqMesh mqMesh = mqObj.Mesh;

            int numVertices = tokenizer.GetInt32();
            tokenizer.EnsureTokens("{");

            for (int i = 0; i < numVertices; ++i)
                mqMesh.AddPosition(tokenizer.GetVector3());

            tokenizer.SkipTokens();
        }

        /// <summary>
        /// フェースサブチャンクの読み込み
        /// </summary>
        MqMesh ReadFaces(MqObject mqObj)
        {
            MqMesh mqMesh = mqObj.Mesh;

            int numFaces = tokenizer.GetInt32();
            if (numFaces == 0)
            {
                tokenizer.SkipChunk();
                return null;
            }

            tokenizer.EnsureTokens("{");

            const int maxVerts = 4;
            int[] faceIndices = new int[maxVerts];
            Vector2[] texCoords = new Vector2[maxVerts];
            Color[] vtxColors = new Color[maxVerts];

            int matIdx = -1;
            bool hasUv = false;
            bool hasVtxColor = false;
            bool hasAlphaVtxColor = false;

            int numVerts = tokenizer.GetInt32();
            for (int faceIdx = 0; faceIdx < numFaces; ++faceIdx)
            {
                bool readFace = false;
                int curNumVerts = numVerts;

                // 一つのフェースデータを読み込む
                while (!readFace)
                {
                    string token = tokenizer.GetToken();
                    switch (token.ToLower())
                    {
                        case "v":
                            for (int i = 0; i < numVerts; ++i)
                                faceIndices[i] = tokenizer.GetInt32();
                            break;
                        case "m":
                            matIdx = tokenizer.GetInt32();
                            break;
                        case "uv":
                            for (int i = 0; i < numVerts; ++i)
                                texCoords[i] = tokenizer.GetVector2();
                            hasUv = true;
                            break;
                        case "col":
                            for (int i = 0; i < numVerts; ++i)
                            {
                                vtxColors[i] = tokenizer.GetColor();
                                if (vtxColors[i].A != 255)
                                    hasAlphaVtxColor = true;
                            }
                            hasVtxColor = true;
                            break;
                        case "}": readFace = true; break;
                        default:
                            // このトークンは頂点数か?
                            if (Int32.TryParse(token, out numVerts))
                                readFace = true;
                            else
                                throw new InvalidOperationException(String.Format(
                                    CultureInfo.CurrentCulture, Resources.UnknownToken,
                                    tokenizer.LineNumber, token));
                            break;
                    }
                }

                // 面をオブジェクトへ追加する
                if (curNumVerts > 1)
                {
                    MqFace face = face = mqObj.Mesh.AddFace(faceIndices, curNumVerts);
                    face.materialIdx = matIdx;
                    face.Material = context.scene.GetMaterial(matIdx);

                    // テクスチャ座標を持っているか？
                    // テクスチャマテリアルがある場合は無条件で持っていることにする
                    face.HasTexcoord = HasTexture(face.Material);

                    // テクスチャ座標があってもマテリアルにテクスチャが内場合は
                    // 持っていないとして扱う
                    if (HasTexture(face.Material) && hasUv)
                        face.HasTexcoord = true;

                    // 頂点カラーを使っているか？
                    BasicMaterialContent mat = face.Material as BasicMaterialContent;
                    if (mat != null && mat.VertexColorEnabled.HasValue &&
                        mat.VertexColorEnabled.Value)
                    {
                        if (hasAlphaVtxColor)
                            mqObj.HasAlphaVertexColor = true;

                        face.HasVertexColor = hasVtxColor;
                    }

                    for (int i = 0; i < curNumVerts; ++i)
                    {
                        if (hasUv) face.Channels[i].Texcoord = texCoords[i];
                        if (hasVtxColor) face.Channels[i].Color = vtxColors[i];
                    }
                }

                // フラグをリセットする
                hasUv = false;
                hasVtxColor = false;
                hasAlphaVtxColor = false;
                matIdx = -1;
            }

            return mqMesh;
        }

        #endregion

        #endregion

        #region ヘルパーメソッド

        /// <summary>
        /// 指定されたマテリアルはテクスチャを使っているか？
        /// </summary>
        public static bool HasTexture(MaterialContent material)
        {
            return material.Textures.Count > 0;
        }

        /// <summary>
        /// テクスチャ参照の生成
        /// </summary>
        ExternalReference<TextureContent> CreateTextureReference(string filename)
        {
            return new ExternalReference<TextureContent>(NormalizeFilepath(filename));
        }

        /// <summary>
        /// テクスチャファイル名のノーマライズ
        /// </summary>
        string NormalizeFilepath(string attemptPath)
        {
            // 指定されたファイルパスは存在するか
            if (File.Exists(attemptPath)) return attemptPath;

            // 指定されたファイルパスはモデルファイルからの相対パスだったか？
            string path = Path.Combine(mqoDirectory, attemptPath);
            if (File.Exists(path)) return path;

            // 後は知らん
            return attemptPath;
        }

        /// <summary>
        /// メタセコイアのマテリアル値からカラー値へ変換する
        /// </summary>
        private static Vector3 GetMaterialColor(Vector4 color, float factor)
        {
            return new Vector3(color.X, color.Y, color.Z) * factor;
        }

        /// <summary>
        /// マテリアルの追加
        /// </summary>
        private void AddMaterial(BasicMaterialContent material,
            Vector4 color, float dif, float emi, float spc, float power)
        {
            material.DiffuseColor = GetMaterialColor(color, dif);
            material.EmissiveColor = GetMaterialColor(color, emi);
            material.SpecularColor = new Vector3(spc);
            material.SpecularPower = power;
            material.Alpha = color.W;

            context.scene.Materials.Add(material);
        }

        #endregion

        #region フィールド

        MqSceneReadContext context;
        bool isDisposed;

        string mqoDirectory;

        MqoTokenizer tokenizer;

        Stack<string> names;

        #endregion

    }
}
