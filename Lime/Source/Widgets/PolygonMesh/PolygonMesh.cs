using System.Collections.Generic;
using Lime.Widgets.PolygonMesh.Topology;
using Yuzu;

namespace Lime.Widgets.PolygonMesh
{
	[TangerineRegisterNode(Order = 32)]
	[TangerineVisualHintGroup("/All/Nodes/Images", "Polygon Mesh")]
	public class PolygonMesh : Widget
	{
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
		// TODO Move to topology
		public List<Edge> ConstrainedEdges { get; set; }

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
			ConstrainedEdges = new List<Edge>();
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
			ro.LocalToWorldTransform = LocalToWorldTransform;
			return ro;
		}

		private unsafe class RenderObject : Lime.RenderObject
		{
			public ITexture Texture;
			public float BlendFactor;
			public int IndexBufferLength;
			public int VertexBufferLength;
			public Matrix32 LocalToWorldTransform;
			public readonly List<ushort> IndexBuffer = new List<ushort>();
			public readonly List<Vertex> VertexBuffer0 = new List<Vertex>();
			public readonly List<Vertex> VertexBuffer1 = new List<Vertex>();

			private static Shader[] shaders;
			private static VertexInputLayoutAttribute[] layoutAttribs;
			private static VertexInputLayoutBinding[] layoutBindings;
			private static VertexInputLayout vertexInputLayout;
			private static ShaderProgram.AttribLocation[] attribLocations;
			private static ShaderProgram.Sampler[] samplers;
			private static ShaderProgram program;

			static RenderObject()
			{
				shaders = new Shader[] {
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
				layoutAttribs = new[] {
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
				layoutBindings = new[] {
					new VertexInputLayoutBinding {
						Slot = 0,
						Stride = sizeof(Vertex),
					},
					new VertexInputLayoutBinding {
						Slot = 1,
						Stride = sizeof(Vertex),
					}
				};
				vertexInputLayout = VertexInputLayout.New(layoutBindings, layoutAttribs);
				attribLocations = new[] {
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
				samplers = new[] {
					new ShaderProgram.Sampler {
						Name = "tex",
						Stage = 0,
					}
				};
				program = new ShaderProgram(shaders, attribLocations, samplers);
			}

			protected override void OnRelease()
			{
				Texture = null;
				BlendFactor = 0.0f;
				IndexBufferLength = 0;
				VertexBufferLength = 0;
				LocalToWorldTransform = Matrix32.Identity;
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
				var shaderParams = new ShaderParams();
				var shaderParamsArray = new[] { shaderParams };
				var blendFactor = shaderParams.GetParamKey<float>("blendFactor");
				var matProjection = shaderParams.GetParamKey<Matrix44>("matProjection");
				var globalTransform = shaderParams.GetParamKey<Matrix44>("globalTransform");
				shaderParams.Set(blendFactor, BlendFactor);
				shaderParams.Set(matProjection, Renderer.FixupWVP(Renderer.WorldViewProjection));
				shaderParams.Set(globalTransform, (Matrix44)(LocalToWorldTransform * Renderer.Transform2));

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
