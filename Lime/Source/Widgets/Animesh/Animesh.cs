using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Yuzu;

namespace Lime.Widgets.Animesh
{
	[TangerineRegisterNode(Order = 32)]
	[TangerineVisualHintGroup("/All/Nodes/Images", "Polygon Mesh")]
	public class Animesh : Widget
	{
		[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 68)]
		public struct SkinnedVertex
		{
			public Vector2 Pos
			{
				get => new Vector2(Pos4.X, Pos4.Y);
				set => Pos4 = new Vector4(value.X, value.Y, 0, 1.0f);
			}

			public SkinningWeights SkinningWeights
			{
				get => new SkinningWeights {
					Bone0 = new BoneWeight { Index = BlendIndices.Index0, Weight = BlendWeights.Weight0 },
					Bone1 = new BoneWeight { Index = BlendIndices.Index1, Weight = BlendWeights.Weight1 },
					Bone2 = new BoneWeight { Index = BlendIndices.Index2, Weight = BlendWeights.Weight2 },
					Bone3 = new BoneWeight { Index = BlendIndices.Index3, Weight = BlendWeights.Weight3 },
				};
				set
				{
					BlendIndices.Index0 = (byte)value.Bone0.Index;
					BlendIndices.Index1 = (byte)value.Bone1.Index;
					BlendIndices.Index2 = (byte)value.Bone2.Index;
					BlendIndices.Index3 = (byte)value.Bone3.Index;

					BlendWeights.Weight0 = value.Bone0.Weight;
					BlendWeights.Weight1 = value.Bone1.Weight;
					BlendWeights.Weight2 = value.Bone2.Weight;
					BlendWeights.Weight3 = value.Bone3.Weight;
				}
			}

			[YuzuMember]
			public Vector4 Pos4;

			[YuzuMember]
			public Color4 Color;

			[YuzuMember]
			public Vector2 UV1;

			[YuzuMember]
			public Mesh3D.BlendIndices BlendIndices;

			[YuzuMember]
			public Mesh3D.BlendWeights BlendWeights;
		}

		[YuzuMember]
		[TangerineStaticProperty]
		public override ITexture Texture { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		public List<Face> Faces { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		public List<SkinnedVertex> Vertices { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		// TODO Move to topology
		public List<Edge> ConstrainedEdges { get; set; }

		/// <summary>
		/// An auxiliary property, which is needed to store values from the <see cref="Animator{T}"/>.
		/// </summary>
		public List<SkinnedVertex> TransientVertices { get; set; }

#if TANGERINE
		public Action OnBoneArrayChanged;
		public bool TangerineAnimationModeEnabled;
#endif

		public Animesh()
		{
			Texture = new SerializableTexture();
			Vertices = new List<SkinnedVertex>();
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
			List<SkinnedVertex> vb0 = null;
			List<SkinnedVertex> vb1 = null;
			var frame = Parent?.DefaultAnimation.Frame ?? 0;
			var lkf = 0;
			var rkf = 0;
#if TANGERINE
			if (TangerineAnimationModeEnabled) {
#endif
				if (Animators.TryFind(nameof(TransientVertices), out var animator)) {
					foreach (var key in animator.Keys) {
						if (key.Frame <= frame) {
							lkf = key.Frame;
							vb0 = key.Value as List<SkinnedVertex>;
						}
						if (key.Frame >= frame) {
							rkf = key.Frame;
							vb1 = key.Value as List<SkinnedVertex>;
							break;
						}
					}
				}
#if TANGERINE
			}
#endif
			if (vb0 == null && vb1 != null) {
				vb0 = new List<SkinnedVertex>(vb1);
				lkf = rkf;
			} else if (vb0 != null && vb1 == null) {
				vb1 = new List<SkinnedVertex>(vb0);
				rkf = lkf;
			} else if (vb0 == null && vb1 == null) {
				vb0 = new List<SkinnedVertex>(Vertices);
				vb1 = new List<SkinnedVertex>(Vertices);
			}
			if (rkf - lkf > 0) {
				var time = Parent.DefaultAnimation.Time;
				var lkt = lkf * AnimationUtils.SecondsPerFrame;
				var rkt = rkf * AnimationUtils.SecondsPerFrame;
				ro.BlendFactor = (float)((time - lkt) / (rkt - lkt));
			}
			ro.BoneTransforms = new Matrix44[50];
			for (var i = 0; i < ro.BoneTransforms.Length; ++i) {
				ro.BoneTransforms[i] = Matrix44.Identity;
			}
			foreach (var vertex in Vertices) {
				// Assuming that parent can't have more than 50 bones.
				ro.BoneTransforms[vertex.BlendIndices.Index0] =
					(Matrix44)ParentWidget.BoneArray[vertex.BlendIndices.Index0].RelativeTransform;
				ro.BoneTransforms[vertex.BlendIndices.Index1] =
					(Matrix44)ParentWidget.BoneArray[vertex.BlendIndices.Index1].RelativeTransform;
				ro.BoneTransforms[vertex.BlendIndices.Index2] =
					(Matrix44)ParentWidget.BoneArray[vertex.BlendIndices.Index2].RelativeTransform;
				ro.BoneTransforms[vertex.BlendIndices.Index3] =
					(Matrix44)ParentWidget.BoneArray[vertex.BlendIndices.Index3].RelativeTransform;
			}
			ro.BoneTransforms[0] = Matrix44.Identity;
			for (var i = 0; i < Vertices.Count; ++i) {
				var v0 = vb0[i];
				var v1 = vb1[i];
				v0.Pos = v0.Pos;
				v1.Pos = v1.Pos;
				if (Texture != null) {
					Texture.TransformUVCoordinatesToAtlasSpace(ref v0.UV1);
					Texture.TransformUVCoordinatesToAtlasSpace(ref v1.UV1);
				}
#if TANGERINE
				if (!TangerineAnimationModeEnabled) {
					v0.SkinningWeights = new SkinningWeights { Bone0 = new BoneWeight { Index = 0, Weight = 1f, }};
					v1.SkinningWeights = new SkinningWeights { Bone0 = new BoneWeight { Index = 0, Weight = 1f, }};
				}
#endif
				ro.VertexBuffer0.Add(v0);
				ro.VertexBuffer1.Add(v1);
			}
			ro.VertexBufferLength = Vertices.Count;
			ro.LocalToWorldTransform = LocalToWorldTransform;
			ro.LocalToParentTransform = CalcLocalToParentTransform();
			ro.ParentToLocalTransform = ro.LocalToParentTransform.CalcInversed();
			return ro;
		}

		private unsafe class RenderObject : Lime.RenderObject
		{
			public ITexture Texture;
			public float BlendFactor;
			public int IndexBufferLength;
			public int VertexBufferLength;
			public Matrix44[] BoneTransforms;
			public Matrix32 LocalToWorldTransform;
			public Matrix32 LocalToParentTransform;
			public Matrix32 ParentToLocalTransform;
			public readonly List<ushort> IndexBuffer = new List<ushort>();
			public readonly List<SkinnedVertex> VertexBuffer0 = new List<SkinnedVertex>();
			public readonly List<SkinnedVertex> VertexBuffer1 = new List<SkinnedVertex>();

			private static readonly VertexInputLayout vertexInputLayout;
			private static readonly ShaderProgram program;

			static RenderObject()
			{
				var shaders = new Shader[] {
					new VertexShader(@"
						#ifdef GL_ES
						precision highp float;
						#endif

						attribute vec4 in_Pos1;
						attribute vec4 in_Pos2;
						attribute vec4 in_Color1;
						attribute vec4 in_Color2;
						attribute vec2 in_UV1;
						attribute vec2 in_UV2;
						attribute vec4 in_BlendIndices;
						attribute vec4 in_BlendWeights;

						varying lowp vec4 color;
						varying lowp vec2 texCoords;

						uniform float u_BlendFactor;
						uniform mat4 u_MVP;
						uniform mat4 u_LocalToParentTransform;
						uniform mat4 u_Bones[50];

						void main()
						{
							color = ((1.0 - u_BlendFactor) * in_Color1 + u_BlendFactor * in_Color2);
							texCoords = (1.0 - u_BlendFactor) * in_UV1 + u_BlendFactor * in_UV2;
							vec4 position = u_LocalToParentTransform * ((1.0 - u_BlendFactor) * in_Pos1 + u_BlendFactor * in_Pos2);
							mat4 skinTransform =
								u_Bones[int(in_BlendIndices.x)] * in_BlendWeights.x +
								u_Bones[int(in_BlendIndices.y)] * in_BlendWeights.y +
								u_Bones[int(in_BlendIndices.z)] * in_BlendWeights.z +
								u_Bones[int(in_BlendIndices.w)] * in_BlendWeights.w;
							position = skinTransform * position;
							gl_Position = u_MVP * position;
						}
					"),
					new FragmentShader(@"
						varying lowp vec4 color;
						varying lowp vec2 texCoords;

						uniform sampler2D u_Tex;

						void main()
						{
							gl_FragColor = color * texture2D(u_Tex, texCoords);
						}
					")
				};
				var layoutAttribs = new[] {
					// in_Pos1
					new VertexInputLayoutAttribute {
						Format = Format.R32G32B32A32_SFloat,
						Slot = 0,
						Location = 0,
						Offset = 0,
					},

					// in_Pos2
					new VertexInputLayoutAttribute {
						Format = Format.R32G32B32A32_SFloat,
						Slot = 1,
						Location = 1,
						Offset = 0,
					},

					// in_Color1
					new VertexInputLayoutAttribute {
						Format = Format.R8G8B8A8_UNorm,
						Slot = 0,
						Location = 2,
						Offset = sizeof(Vector4),
					},

					// in_Color2
					new VertexInputLayoutAttribute {
						Format = Format.R8G8B8A8_UNorm,
						Slot = 1,
						Location = 3,
						Offset = sizeof(Vector4),
					},

					// in_UV1
					new VertexInputLayoutAttribute {
						Format = Format.R32G32_SFloat,
						Slot = 0,
						Location = 4,
						Offset = sizeof(Vector4) + sizeof(Color4),
					},

					// in_UV2
					new VertexInputLayoutAttribute {
						Format = Format.R32G32_SFloat,
						Slot = 1,
						Location = 5,
						Offset = sizeof(Vector4) + sizeof(Color4),
					},

					// in_BlendIndices
					new VertexInputLayoutAttribute {
						Format = Format.R32G32B32A32_SFloat,
						Slot = 0,
						Location = 6,
						Offset = sizeof(Vector4) + sizeof(Color4) + sizeof(Vector2),
					},

					// in_BlendWeights
					new VertexInputLayoutAttribute {
						Format = Format.R32G32B32A32_SFloat,
						Slot = 0,
						Location = 7,
						Offset = 2 * sizeof(Vector4) + sizeof(Color4) + sizeof(Vector2),
					}
				};
				var layoutBindings = new[] {
					new VertexInputLayoutBinding {
						Slot = 0,
						Stride = sizeof(SkinnedVertex),
					},
					new VertexInputLayoutBinding {
						Slot = 1,
						Stride = sizeof(SkinnedVertex),
					},
				};
				vertexInputLayout = VertexInputLayout.New(layoutBindings, layoutAttribs);
				var attribLocations = new[] {
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
						Name = "in_UV1",
						Index = 4,
					},
					new ShaderProgram.AttribLocation {
						Name = "in_UV2",
						Index = 5,
					},
					new ShaderProgram.AttribLocation {
						Name = "in_BlendIndices",
						Index = 6,
					},
					new ShaderProgram.AttribLocation {
						Name = "in_BlendWeights",
						Index = 7,
					}
				};
				var samplers = new[] {
					new ShaderProgram.Sampler {
						Name = "u_Tex",
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
				BoneTransforms = null;
				LocalToWorldTransform = Matrix32.Identity;
				LocalToParentTransform = Matrix32.Identity;
				ParentToLocalTransform = Matrix32.Identity;
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

				var vb0Data = new SkinnedVertex[VertexBufferLength];
				var vb1Data = new SkinnedVertex[VertexBufferLength];

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
				var blendFactor = shaderParams.GetParamKey<float>("u_BlendFactor");
				var mvpTransform = shaderParams.GetParamKey<Matrix44>("u_MVP");
				var localToParentTransform = shaderParams.GetParamKey<Matrix44>("u_LocalToParentTransform");
				var bones = shaderParams.GetParamKey<Matrix44>("u_Bones");
				shaderParams.Set(blendFactor, BlendFactor);
				shaderParams.Set(
					mvpTransform,
					(Matrix44)ParentToLocalTransform * (Matrix44)(LocalToWorldTransform * Renderer.Transform2) *
					Renderer.FixupWVP(Renderer.WorldViewProjection)
				);
				shaderParams.Set(localToParentTransform, (Matrix44)LocalToParentTransform);
				shaderParams.Set(bones, BoneTransforms, BoneTransforms.Length);

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
