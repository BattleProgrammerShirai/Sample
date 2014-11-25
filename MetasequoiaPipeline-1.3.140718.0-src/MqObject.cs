#region ファイル説明
//-----------------------------------------------------------------------------
// MqObject.cs
//=============================================================================
#endregion

#region Using ステートメント

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

#endregion

namespace MetasequoiaPipeline
{
    /// <summary>
    /// メタセコイアのオブジェクト情報
    /// </summary>
    public class MqObject
    {

        #region プロパティ

        /// <summary>
        /// このオブジェクトを含むシーンの取得
        /// </summary>
        public MqScene Scene { get; internal set; }

        /// <summary>
        /// オブジェクト名の取得と設定
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 親オブジェクト名の取得と設定
        /// </summary>
        [ContentSerializer(Optional = true)]
        public string Parent { get; set; }

        /// <summary>
        /// スムージングアングルの取得と設定
        /// </summary>
        [ContentSerializerIgnore]
        public float? SmoothAngle { get; set; }

        [ContentSerializer(ElementName = "SmoothAngle", Optional = true)]
        internal float _serializableSmoothAngle
        {
            get { return MathHelper.ToDegrees(SmoothAngle.Value); }
            set { SmoothAngle = MathHelper.ToRadians(value); }
        }

        /// <summary>
        /// 可視情報の取得と設定
        /// </summary>
        [ContentSerializer(Optional = true)]
        public bool IsVisible { get; set; }

        /// <summary>
        /// 面タイプの取得と設定
        /// </summary>
        [ContentSerializer(Optional = true)]
        public MqPatchType PatchType { get; set; }

        /// <summary>
        /// 面分割数の取得と設定
        /// </summary>
        [ContentSerializer(Optional = true)]
        public int PatchSegments { get; set; }

        /// <summary>
        /// オブジェクトトランスフォームの取得
        /// </summary>
        public Matrix Transform { get; private set; }

        /// <summary>
        /// ミラーリング情報の取得と設定
        /// </summary>
        [ContentSerializer(Optional = true)]
        public MqMirrorSettings MirrorSettings { get; set; }

        /// <summary>
        /// 回転体情報の取得と設定
        /// </summary>
        [ContentSerializer(Optional = true)]
        public MqLatheSettings LatheSettings { get; set; }

        /// <summary>
        /// アルファ値を使った頂点カラーを使用しているか？
        /// </summary>
        public bool HasAlphaVertexColor { get; internal set; }


        #region インターナルプロパティ

        /// <summary>
        /// 平行移動値の取得と設定
        /// </summary>
        [ContentSerializer]
        internal Vector3 Translation
        {
            get { return _translation; }
            set { _translation = value; ComputeTransform(); }
        }

        Vector3 _translation;

        /// <summary>
        /// 回転値の取得と設定
        /// </summary>
        [ContentSerializerIgnore]
        internal Vector3 Rotation
        {
            get { return _rotation; }
            set { _rotation = value; ComputeTransform(); }
        }

        Vector3 _rotation;

        /// <summary>
        /// MKXファイル内では角度、メモリ内ではラジアンで形式なので
        /// シリアライズ時に変換作業をする為のダミープロパティ
        /// </summary>
        [ContentSerializer(ElementName = "Rotation")]
        internal Vector3 _serializableRotation
        {
            get
            {
                return new Vector3(
                    MathHelper.ToDegrees(Rotation.X),
                    MathHelper.ToDegrees(Rotation.Y),
                    MathHelper.ToDegrees(Rotation.Z));
            }

            set
            {
                Rotation = new Vector3(
                    MathHelper.ToRadians(value.X),
                    MathHelper.ToRadians(value.Y),
                    MathHelper.ToRadians(value.Z));
            }
        }

        /// <summary>
        /// スケール値の取得と設定
        /// </summary>
        [ContentSerializer]
        internal Vector3 Scale
        {
            get { return _scale; }
            set { _scale = value; ComputeTransform(); }
        }

        Vector3 _scale = new Vector3(1);

        /// <summary>
        /// スムーズ制限角度のCos値、頂点法線計算で使われる
        /// </summary>
        internal float SmoothLimit { get; set; }

        /// <summary>
        /// スムーズ制限角度の減衰終端値、頂点法線計算で使われる
        /// </summary>
        internal float SmoothFallout { get; set; }


        /// <summary>
        /// メッシュ情報の取得と設定
        /// </summary>
        [ContentSerializer(Optional = true)]
        public MqMesh Mesh { get; set; }

        #endregion

        #endregion

        /// <summary>
        /// 生成
        /// </summary>
        public MqObject()
        {
            SmoothAngle = MathHelper.Pi;
            IsVisible = true;
            PatchType = MqPatchType.Polygon;

            ComputeTransform();
        }

        /// <summary>
        /// 生成
        /// </summary>
        public MqObject(MqScene scene, string name)
            : this()
        {
            this.Scene = scene;
            this.Name = name;
        }

        #region プライベートメソッド

        internal void EnsureLatheSettings()
        {
            if (LatheSettings == null)
                LatheSettings = new MqLatheSettings();
        }

        internal void EnsureMirrorSettings()
        {
            if (MirrorSettings == null)
                MirrorSettings = new MqMirrorSettings();
        }

        /// <summary>
        /// トランスフォーム値を計算する
        /// </summary>
        void ComputeTransform()
        {
            Transform =
                Matrix.CreateScale(Scale) *
                Matrix.CreateFromYawPitchRoll(
                        MathHelper.ToRadians(Rotation.Y),
                        MathHelper.ToRadians(Rotation.X),
                        MathHelper.ToRadians(Rotation.Z)) *
                Matrix.CreateTranslation(Translation);
        }

        #endregion

    }
}
