using System;
using System.Collections.Generic;

#if !iOS && !MAC && !ANDROID
using OpenTK.Graphics.ES20;
#endif

namespace Lime.Graphics.Platform.OpenGL
{
	internal class PlatformVertexInputLayout : IPlatformVertexInputLayout
	{
		internal GLVertexInputLayoutBinding[] GLBindings;
		internal long BindingMask;
		internal long AttributeMask;

		public PlatformRenderContext Context { get; }

		internal PlatformVertexInputLayout(
			PlatformRenderContext context,
			VertexInputLayoutBinding[] bindings,
			VertexInputLayoutAttribute[] attributes)
		{
			Context = context;
			Initialize(bindings, attributes);
		}

		private void Initialize(VertexInputLayoutBinding[] bindings, VertexInputLayoutAttribute[] attributes)
		{
			var glBindingAttribs = new List<GLVertexInputLayoutAttribute>[bindings.Length];
			foreach (var attrib in attributes) {
				var bindingIndex = Array.FindIndex(bindings, binding => binding.Slot == attrib.Slot);
				if (bindingIndex < 0) {
					continue;
				}
				GLHelper.GetGLVertexAttribFormat(attrib.Format, out var glType, out var glSize, out var glNormalized);
				if (glBindingAttribs[bindingIndex] == null) {
					glBindingAttribs[bindingIndex] = new List<GLVertexInputLayoutAttribute>();
				}
				glBindingAttribs[bindingIndex].Add(new GLVertexInputLayoutAttribute {
					Index = attrib.Location,
					Offset = attrib.Offset,
					Type = glType,
					Size = glSize,
					Normalized = glNormalized,
				});
			}
			var glBindings = new List<GLVertexInputLayoutBinding>();
			for (var i = 0; i < bindings.Length; i++) {
				var glAttribs = glBindingAttribs[i];
				if (glAttribs != null) {
					glBindings.Add(new GLVertexInputLayoutBinding {
						Slot = bindings[i].Slot,
						Stride = bindings[i].Stride,
						Attributes = glAttribs.ToArray(),
					});
				}
			}
			GLBindings = glBindings.ToArray();
			foreach (var binding in GLBindings) {
				BindingMask |= 1L << binding.Slot;
				foreach (var attrib in binding.Attributes) {
					AttributeMask |= 1L << attrib.Index;
				}
			}
		}

		public void Dispose() { }
	}

	internal struct GLVertexInputLayoutBinding
	{
		public int Slot;
		public int Stride;
		public GLVertexInputLayoutAttribute[] Attributes;
	}

	internal struct GLVertexInputLayoutAttribute
	{
		public int Index;
		public int Offset;
		public int Size;
		public bool Normalized;
		public All Type;
	}
}
