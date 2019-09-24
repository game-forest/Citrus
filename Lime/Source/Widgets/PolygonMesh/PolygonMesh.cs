using System;
using System.Collections.Generic;
using Yuzu;

namespace Lime.PolygonMesh
{
	[TangerineRegisterNode(Order = 32)]
	[TangerineVisualHintGroup("/All/Nodes/Images", "Polygon Mesh")]
	public class PolygonMesh : Widget
	{
		[YuzuCompact]
		public struct Edge : IEquatable<Edge>
		{
			[YuzuMember("0")]
			public ushort Index0;

			[YuzuMember("1")]
			public ushort Index1;

			public ushort this[int index]
			{
				get
				{
					switch (index) {
						case 0:
							return Index0;
						case 1:
							return Index1;
					}
					throw new IndexOutOfRangeException();
				}
			}

			public Edge(ushort index0, ushort index1)
			{
				Index0 = index0;
				Index1 = index1;
			}

			public bool Equals(Edge other)
			{
				return
					(Index0 == other.Index0 && Index1 == other.Index1) ||
					(Index0 == other.Index1 && Index1 == other.Index0);
			}

			public override int GetHashCode()
			{
				return
					(Index0, Index1).GetHashCode() +
					(Index1, Index0).GetHashCode();
			}
		}

		[YuzuCompact]
		public struct Face : IEquatable<Face>
		{
			[YuzuMember("0")]
			public ushort Index0;

			[YuzuMember("1")]
			public ushort Index1;

			[YuzuMember("2")]
			public ushort Index2;

			public int this[int index]
			{
				get {
					switch (index) {
						case 0:
							return Index0;
						case 1:
							return Index1;
						case 2:
							return Index2;
					}
					throw new IndexOutOfRangeException();
				}
			}

			public bool Equals(Face other) =>
				Index0 == other.Index0 &&
				Index1 == other.Index1 &&
				Index2 == other.Index2 ||

				Index0 == other.Index0 &&
				Index1 == other.Index2 &&
				Index2 == other.Index1 ||

				Index1 == other.Index1 &&
				Index0 == other.Index2 &&
				Index2 == other.Index0 ||

				Index2 == other.Index2 &&
				Index0 == other.Index1 &&
				Index1 == other.Index0;

			public override int GetHashCode()
			{
				return
					(Index0, Index1, Index2).GetHashCode() +
					(Index2, Index0, Index1).GetHashCode() +
					(Index1, Index2, Index0).GetHashCode();
			}
		}

		[YuzuMember]
		[TangerineStaticProperty]
		public override ITexture Texture { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		public List<Face> Faces { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		public List<Vertex> Vertices { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		public List<Edge> ConstrainedVertices { get; set; }

		public List<Vertex> TransientVertices { get; set; }

#if TANGERINE
		public enum ModificationMode
		{
			Animation,
			Setup
		}

		public ModificationMode Mode { get; set; } 
#endif

		public PolygonMesh()
		{
			Texture = new SerializableTexture();
			Vertices = new List<Vertex>();
			Faces = new List<Face>();
			ConstrainedVertices = new List<Edge>();
		}

		public override void AddToRenderChain(RenderChain chain)
		{
			if (GloballyVisible && ClipRegionTest(chain.ClipRegion)) {
				AddSelfToRenderChain(chain, Layer);
			}
		}

		protected internal override Lime.RenderObject GetRenderObject()
		{
			var ro = RenderObjectPool<RenderObject>.Acquire();
			ro.Texture = Texture;
			ro.IndexBufferLength = Faces.Count * 3;
			foreach (var f in Faces) {
				ro.IndexBuffer.Add(f.Index0);
				ro.IndexBuffer.Add(f.Index1);
				ro.IndexBuffer.Add(f.Index2);
			}
			List<Vertex> vb0 = null;
			List<Vertex> vb1 = null;
			var frame = Parent?.DefaultAnimation.Frame ?? 0;
			var lkf = 0;
			var rkf = 0;
#if TANGERINE
			if (Mode == ModificationMode.Animation) {
#endif
				if (Animators.TryFind(nameof(TransientVertices), out var animator)) {
					foreach (var key in animator.Keys) {
						if (key.Frame <= frame) {
							lkf = key.Frame;
							vb0 = key.Value as List<Vertex>;
						}
						if (key.Frame >= frame) {
							rkf = key.Frame;
							vb1 = key.Value as List<Vertex>;
							break;
						}
					}
				}
#if TANGERINE
			}
#endif
			if (vb0 == null && vb1 != null) {
				vb0 = new List<Vertex>(vb1);
				lkf = rkf;
			} else if (vb0 != null && vb1 == null) {
				vb1 = new List<Vertex>(vb0);
				rkf = lkf;
			} else if (vb0 == null && vb1 == null) {
				vb0 = new List<Vertex>(Vertices);
				vb1 = new List<Vertex>(Vertices);
			}
			if (rkf - lkf > 0) {
				var time = Parent.DefaultAnimation.Time;
				var lkt = lkf * AnimationUtils.SecondsPerFrame;
				var rkt = rkf * AnimationUtils.SecondsPerFrame;
				ro.BlendFactor = (float)((time - lkt) / (rkt - lkt));
			}
			for (var i = 0; i < Vertices.Count; ++i) {
				var v0 = vb0[i];
				v0.Pos = v0.Pos * Size;
				var v1 = vb1[i];
				v1.Pos = v1.Pos * Size;
				ro.VertexBuffer0.Add(v0);
				ro.VertexBuffer1.Add(v1);
			}
			ro.VertexBufferLength = Vertices.Count;
			var m = LocalToWorldTransform;
			ro.LocalToWorldTransform44 = new Matrix44 {
				M11 = m.UX, M12 = m.UY, M13 = 0, M14 = 0,
				M21 = m.VX, M22 = m.VY, M23 = 0, M24 = 0,
				M31 =    0, M32 =    0, M33 = 1, M34 = 0,
				M41 = m.TX, M42 = m.TY, M43 = 0, M44 = 1
			};
			return ro;
		}

		private unsafe class RenderObject : Lime.RenderObject
		{
			public ITexture Texture;
			public float BlendFactor;
			public int IndexBufferLength;
			public int VertexBufferLength;
			public Matrix44 LocalToWorldTransform44;
			public readonly List<ushort> IndexBuffer = new List<ushort>();
			public readonly List<Vertex> VertexBuffer0 = new List<Vertex>();
			public readonly List<Vertex> VertexBuffer1 = new List<Vertex>();

			protected override void OnRelease()
			{
				Texture = null;
				BlendFactor = 0.0f;
				IndexBufferLength = 0;
				VertexBufferLength = 0;
				LocalToWorldTransform44 = Matrix44.Identity;
				IndexBuffer.Clear();
				VertexBuffer0.Clear();
				VertexBuffer1.Clear();
			}

			public override void Render()
			{
				if (IndexBufferLength == 0) {
					return;
				}
				var vb0 = new VertexBuffer(false);
				var vb1 = new VertexBuffer(false);

				var vb0Data = new Vertex[VertexBufferLength];
				var vb1Data = new Vertex[VertexBufferLength];

				for (var i = 0; i < VertexBufferLength; ++i) {
					vb0Data[i] = VertexBuffer0[i];
					vb1Data[i] = VertexBuffer1[i];
				}

				vb0.SetData(vb0Data, VertexBufferLength);
				vb1.SetData(vb1Data, VertexBufferLength);

				var ib = new IndexBuffer(false);
				var ibData = new ushort[IndexBufferLength];
				for (var i = 0; i < IndexBuffer.Count; ++i) {
					ibData[i] = IndexBuffer[i];
				}
				ib.SetData(ibData, IndexBufferLength);

				var layoutAttribs = new[] {
					new VertexInputLayoutAttribute {
						Format = Format.R32G32B32A32_SFloat,
						Slot = 0,
						Location = 0,
						Offset = 0,
					},
					new VertexInputLayoutAttribute {
						Format = Format.R32G32B32A32_SFloat,
						Slot = 1,
						Location = 1,
						Offset = 0,
					},
					new VertexInputLayoutAttribute {
						Format = Format.R8G8B8A8_UNorm,
						Slot = 0,
						Location = 2,
						Offset = sizeof(Vector4),
					},
					new VertexInputLayoutAttribute {
						Format = Format.R8G8B8A8_UNorm,
						Slot = 1,
						Location = 3,
						Offset = sizeof(Vector4),
					},
					new VertexInputLayoutAttribute {
						Format = Format.R32G32B32_SFloat,
						Slot = 0,
						Location = 4,
						Offset = sizeof(Vector4) + sizeof(Color4),
					},
					new VertexInputLayoutAttribute {
						Format = Format.R32G32B32_SFloat,
						Slot = 1,
						Location = 5,
						Offset = sizeof(Vector4) + sizeof(Color4),
					}
				};

				var layoutBindings = new[] {
					new VertexInputLayoutBinding {
						Slot = 0,
						Stride = sizeof(Vertex),
					},
					new VertexInputLayoutBinding {
						Slot = 1,
						Stride = sizeof(Vertex),
					}
				};

				var vertexInputLayout = VertexInputLayout.New(layoutBindings, layoutAttribs);

				var shaders = new Shader[] {
					new VertexShader(@"
						attribute vec4 in_Pos1;
						attribute vec4 in_Pos2;
						attribute vec4 in_Color1;
						attribute vec4 in_Color2;
						attribute vec2 in_TexCoords1;
						attribute vec2 in_TexCoords2;

						uniform float blendFactor;
						uniform mat4 matProjection;
						uniform mat4 globalTransform;

						varying lowp vec4 color;
						varying lowp vec2 texCoords;

						void main()
						{
							color = (1.0 - blendFactor) * in_Color1 + blendFactor * in_Color2;
							texCoords = (1.0 - blendFactor) * in_TexCoords1 + blendFactor * in_TexCoords2;
							gl_Position = matProjection * (globalTransform * ((1.0 - blendFactor) * in_Pos1 + blendFactor * in_Pos2));
						}
					"),
					new FragmentShader(@"
						uniform sampler2D tex;

						varying lowp vec4 color;
						varying lowp vec2 texCoords;

						void main()
						{
							gl_FragColor = color * texture2D(tex, texCoords);
						}
					")
				};

				var attribLocations = new ShaderProgram.AttribLocation[] {
					new ShaderProgram.AttribLocation {
						Name = "in_Pos1",
						Index = 0,
					},
					new ShaderProgram.AttribLocation {
						Name = "in_Pos2",
						Index = 1
					},
					new ShaderProgram.AttribLocation {
						Name = "in_Color1",
						Index = 2,
					},
					new ShaderProgram.AttribLocation {
						Name = "in_Color1",
						Index = 3,
					},
					new ShaderProgram.AttribLocation {
						Name = "in_TexCoords1",
						Index = 4,
					},
					new ShaderProgram.AttribLocation {
						Name = "in_TexCoords2",
						Index = 5,
					}
				};

				var samplers = new ShaderProgram.Sampler[] {
					new ShaderProgram.Sampler {
						Name = "tex",
						Stage = 0,
					}
				};

				var program = new ShaderProgram(shaders, attribLocations, samplers);
				var shaderParams = new ShaderParams();
				var shaderParamsArray = new[] { shaderParams };
				var blendFactor = shaderParams.GetParamKey<float>("blendFactor");
				var matProjection = shaderParams.GetParamKey<Matrix44>("matProjection");
				var globalTransform = shaderParams.GetParamKey<Matrix44>("globalTransform");
				shaderParams.Set(blendFactor, BlendFactor);
				shaderParams.Set(matProjection, Renderer.FixupWVP(Renderer.WorldViewProjection));
				shaderParams.Set(globalTransform, LocalToWorldTransform44);

				Renderer.Flush();
				PlatformRenderer.SetTexture(0, Texture);
				PlatformRenderer.SetIndexBuffer(ib, 0, IndexFormat.Index16Bits);
				PlatformRenderer.SetVertexBuffer(0, vb0, 0);
				PlatformRenderer.SetVertexBuffer(1, vb1, 0);
				PlatformRenderer.SetVertexInputLayout(vertexInputLayout);
				PlatformRenderer.SetShaderProgram(program);
				PlatformRenderer.SetShaderParams(shaderParamsArray);
				PlatformRenderer.DrawIndexed(PrimitiveTopology.TriangleList, 0, IndexBufferLength);
			}
		}
	}
}
