#if iOS || MAC || ANDROID
using System;

namespace Lime.Graphics.Platform.OpenGL
{
	#pragma warning disable 1591

	public enum All : int
	{
		False = ((int)0),
		NoError = ((int)0),
		None = ((int)0),
		Zero = ((int)0),
		Points = ((int)0x0000),
		DepthBufferBit = ((int)0x00000100),
		StencilBufferBit = ((int)0x00000400),
		ColorBufferBit = ((int)0x00004000),
		Lines = ((int)0x0001),
		LineLoop = ((int)0x0002),
		LineStrip = ((int)0x0003),
		Triangles = ((int)0x0004),
		TriangleStrip = ((int)0x0005),
		TriangleFan = ((int)0x0006),
		Never = ((int)0x0200),
		Less = ((int)0x0201),
		Equal = ((int)0x0202),
		Lequal = ((int)0x0203),
		Greater = ((int)0x0204),
		Notequal = ((int)0x0205),
		Gequal = ((int)0x0206),
		Always = ((int)0x0207),
		SrcColor = ((int)0x0300),
		OneMinusSrcColor = ((int)0x0301),
		SrcAlpha = ((int)0x0302),
		OneMinusSrcAlpha = ((int)0x0303),
		DstAlpha = ((int)0x0304),
		OneMinusDstAlpha = ((int)0x0305),
		DstColor = ((int)0x0306),
		OneMinusDstColor = ((int)0x0307),
		SrcAlphaSaturate = ((int)0x0308),
		Front = ((int)0x0404),
		Back = ((int)0x0405),
		FrontAndBack = ((int)0x0408),
		InvalidEnum = ((int)0x0500),
		InvalidValue = ((int)0x0501),
		InvalidOperation = ((int)0x0502),
		OutOfMemory = ((int)0x0505),
		InvalidFramebufferOperation = ((int)0x0506),
		Cw = ((int)0x0900),
		Ccw = ((int)0x0901),
		LineWidth = ((int)0x0B21),
		CullFace = ((int)0x0B44),
		CullFaceMode = ((int)0x0B45),
		FrontFace = ((int)0x0B46),
		DepthRange = ((int)0x0B70),
		DepthTest = ((int)0x0B71),
		DepthWritemask = ((int)0x0B72),
		DepthClearValue = ((int)0x0B73),
		DepthFunc = ((int)0x0B74),
		StencilTest = ((int)0x0B90),
		StencilClearValue = ((int)0x0B91),
		StencilFunc = ((int)0x0B92),
		StencilValueMask = ((int)0x0B93),
		StencilFail = ((int)0x0B94),
		StencilPassDepthFail = ((int)0x0B95),
		StencilPassDepthPass = ((int)0x0B96),
		StencilRef = ((int)0x0B97),
		StencilWritemask = ((int)0x0B98),
		Viewport = ((int)0x0BA2),
		Dither = ((int)0x0BD0),
		Blend = ((int)0x0BE2),
		ScissorBox = ((int)0x0C10),
		ScissorTest = ((int)0x0C11),
		ColorClearValue = ((int)0x0C22),
		ColorWritemask = ((int)0x0C23),
		UnpackAlignment = ((int)0x0CF5),
		PackAlignment = ((int)0x0D05),
		MaxTextureSize = ((int)0x0D33),
		MaxViewportDims = ((int)0x0D3A),
		SubpixelBits = ((int)0x0D50),
		RedBits = ((int)0x0D52),
		GreenBits = ((int)0x0D53),
		BlueBits = ((int)0x0D54),
		AlphaBits = ((int)0x0D55),
		DepthBits = ((int)0x0D56),
		StencilBits = ((int)0x0D57),
		Texture2D = ((int)0x0DE1),
		DontCare = ((int)0x1100),
		Fastest = ((int)0x1101),
		Nicest = ((int)0x1102),
		Byte = ((int)0x1400),
		UnsignedByte = ((int)0x1401),
		Short = ((int)0x1402),
		UnsignedShort = ((int)0x1403),
		Int = ((int)0x1404),
		UnsignedInt = ((int)0x1405),
		Float = ((int)0x1406),
		Fixed = ((int)0x140C),
		Invert = ((int)0x150A),
		Texture = ((int)0x1702),
		StencilIndex = ((int)0x1901),
		DepthComponent = ((int)0x1902),
		Alpha = ((int)0x1906),
		Rgb = ((int)0x1907),
		Rgba = ((int)0x1908),
		RgExt = ((int)0x8227),
		Red = ((int)0x1903),
		Luminance = ((int)0x1909),
		LuminanceAlpha = ((int)0x190A),
		Keep = ((int)0x1E00),
		Replace = ((int)0x1E01),
		Incr = ((int)0x1E02),
		Decr = ((int)0x1E03),
		Vendor = ((int)0x1F00),
		Renderer = ((int)0x1F01),
		Version = ((int)0x1F02),
		Extensions = ((int)0x1F03),
		Nearest = ((int)0x2600),
		Linear = ((int)0x2601),
		NearestMipmapNearest = ((int)0x2700),
		LinearMipmapNearest = ((int)0x2701),
		NearestMipmapLinear = ((int)0x2702),
		LinearMipmapLinear = ((int)0x2703),
		TextureMagFilter = ((int)0x2800),
		TextureMinFilter = ((int)0x2801),
		TextureWrapS = ((int)0x2802),
		TextureWrapT = ((int)0x2803),
		Repeat = ((int)0x2901),
		PolygonOffsetUnits = ((int)0x2A00),
		ConstantColor = ((int)0x8001),
		OneMinusConstantColor = ((int)0x8002),
		ConstantAlpha = ((int)0x8003),
		OneMinusConstantAlpha = ((int)0x8004),
		BlendColor = ((int)0x8005),
		FuncAdd = ((int)0x8006),
		BlendEquation = ((int)0x8009),
		FuncSubtract = ((int)0x800A),
		FuncReverseSubtract = ((int)0x800B),
		UnsignedShort4444 = ((int)0x8033),
		UnsignedShort5551 = ((int)0x8034),
		PolygonOffsetFill = ((int)0x8037),
		PolygonOffsetFactor = ((int)0x8038),
		Rgb8Oes = ((int)0x8051),
		Rgba4 = ((int)0x8056),
		Rgb5A1 = ((int)0x8057),
		Rgba8Oes = ((int)0x8058),
		TextureBinding2D = ((int)0x8069),
		TextureBinding3DOes = ((int)0x806A),
		Texture3DOes = ((int)0x806F),
		TextureWrapROes = ((int)0x8072),
		Max3DTextureSizeOes = ((int)0x8073),
		SampleAlphaToCoverage = ((int)0x809E),
		SampleCoverage = ((int)0x80A0),
		SampleBuffers = ((int)0x80A8),
		Samples = ((int)0x80A9),
		SampleCoverageValue = ((int)0x80AA),
		SampleCoverageInvert = ((int)0x80AB),
		BlendDstRgb = ((int)0x80C8),
		BlendSrcRgb = ((int)0x80C9),
		BlendDstAlpha = ((int)0x80CA),
		BlendSrcAlpha = ((int)0x80CB),
		BgraExt = ((int)0x80E1),
		ClampToEdge = ((int)0x812F),
		GenerateMipmapHint = ((int)0x8192),
		DepthComponent16 = ((int)0x81A5),
		DepthComponent24Oes = ((int)0x81A6),
		DepthComponent32Oes = ((int)0x81A7),
		UnsignedShort565 = ((int)0x8363),
		UnsignedShort4444Rev = ((int)0x8365),
		UnsignedShort1555Rev = ((int)0x8366),
		UnsignedInt2101010RevExt = ((int)0x8368),
		MirroredRepeat = ((int)0x8370),
		AliasedPointSizeRange = ((int)0x846D),
		AliasedLineWidthRange = ((int)0x846E),
		Texture0 = ((int)0x84C0),
		Texture1 = ((int)0x84C1),
		Texture2 = ((int)0x84C2),
		Texture3 = ((int)0x84C3),
		Texture4 = ((int)0x84C4),
		Texture5 = ((int)0x84C5),
		Texture6 = ((int)0x84C6),
		Texture7 = ((int)0x84C7),
		Texture8 = ((int)0x84C8),
		Texture9 = ((int)0x84C9),
		Texture10 = ((int)0x84CA),
		Texture11 = ((int)0x84CB),
		Texture12 = ((int)0x84CC),
		Texture13 = ((int)0x84CD),
		Texture14 = ((int)0x84CE),
		Texture15 = ((int)0x84CF),
		Texture16 = ((int)0x84D0),
		Texture17 = ((int)0x84D1),
		Texture18 = ((int)0x84D2),
		Texture19 = ((int)0x84D3),
		Texture20 = ((int)0x84D4),
		Texture21 = ((int)0x84D5),
		Texture22 = ((int)0x84D6),
		Texture23 = ((int)0x84D7),
		Texture24 = ((int)0x84D8),
		Texture25 = ((int)0x84D9),
		Texture26 = ((int)0x84DA),
		Texture27 = ((int)0x84DB),
		Texture28 = ((int)0x84DC),
		Texture29 = ((int)0x84DD),
		Texture30 = ((int)0x84DE),
		Texture31 = ((int)0x84DF),
		ActiveTexture = ((int)0x84E0),
		MaxRenderbufferSize = ((int)0x84E8),
		AllCompletedNv = ((int)0x84F2),
		FenceStatusNv = ((int)0x84F3),
		FenceConditionNv = ((int)0x84F4),
		DepthStencilOes = ((int)0x84F9),
		UnsignedInt248Oes = ((int)0x84FA),
		TextureMaxAnisotropyExt = ((int)0x84FE),
		MaxTextureMaxAnisotropyExt = ((int)0x84FF),
		IncrWrap = ((int)0x8507),
		DecrWrap = ((int)0x8508),
		TextureCubeMap = ((int)0x8513),
		TextureBindingCubeMap = ((int)0x8514),
		TextureCubeMapPositiveX = ((int)0x8515),
		TextureCubeMapNegativeX = ((int)0x8516),
		TextureCubeMapPositiveY = ((int)0x8517),
		TextureCubeMapNegativeY = ((int)0x8518),
		TextureCubeMapPositiveZ = ((int)0x8519),
		TextureCubeMapNegativeZ = ((int)0x851A),
		MaxCubeMapTextureSize = ((int)0x851C),
		VertexAttribArrayEnabled = ((int)0x8622),
		VertexAttribArraySize = ((int)0x8623),
		VertexAttribArrayStride = ((int)0x8624),
		VertexAttribArrayType = ((int)0x8625),
		CurrentVertexAttrib = ((int)0x8626),
		VertexAttribArrayPointer = ((int)0x8645),
		NumCompressedTextureFormats = ((int)0x86A2),
		CompressedTextureFormats = ((int)0x86A3),
		Z400BinaryAmd = ((int)0x8740),
		ProgramBinaryLengthOes = ((int)0x8741),
		BufferSize = ((int)0x8764),
		BufferUsage = ((int)0x8765),
		AtcRgbaInterpolatedAlphaAmd = ((int)0x87EE),
		GL_3DcXAmd = ((int)0x87F9),
		GL_3DcXyAmd = ((int)0x87FA),
		NumProgramBinaryFormatsOes = ((int)0x87FE),
		ProgramBinaryFormatsOes = ((int)0x87FF),
		StencilBackFunc = ((int)0x8800),
		StencilBackFail = ((int)0x8801),
		StencilBackPassDepthFail = ((int)0x8802),
		StencilBackPassDepthPass = ((int)0x8803),
		BlendEquationAlpha = ((int)0x883D),
		MaxVertexAttribs = ((int)0x8869),
		VertexAttribArrayNormalized = ((int)0x886A),
		MaxTextureImageUnits = ((int)0x8872),
		ArrayBuffer = ((int)0x8892),
		ElementArrayBuffer = ((int)0x8893),
		ArrayBufferBinding = ((int)0x8894),
		ElementArrayBufferBinding = ((int)0x8895),
		VertexAttribArrayBufferBinding = ((int)0x889F),
		WriteOnlyOes = ((int)0x88B9),
		BufferAccessOes = ((int)0x88BB),
		BufferMappedOes = ((int)0x88BC),
		BufferMapPointerOes = ((int)0x88BD),
		StreamDraw = ((int)0x88E0),
		StaticDraw = ((int)0x88E4),
		DynamicDraw = ((int)0x88E8),
		Depth24Stencil8Oes = ((int)0x88F0),
		FragmentShader = ((int)0x8B30),
		VertexShader = ((int)0x8B31),
		MaxVertexTextureImageUnits = ((int)0x8B4C),
		MaxCombinedTextureImageUnits = ((int)0x8B4D),
		ShaderType = ((int)0x8B4F),
		FloatVec2 = ((int)0x8B50),
		FloatVec3 = ((int)0x8B51),
		FloatVec4 = ((int)0x8B52),
		IntVec2 = ((int)0x8B53),
		IntVec3 = ((int)0x8B54),
		IntVec4 = ((int)0x8B55),
		Bool = ((int)0x8B56),
		BoolVec2 = ((int)0x8B57),
		BoolVec3 = ((int)0x8B58),
		BoolVec4 = ((int)0x8B59),
		FloatMat2 = ((int)0x8B5A),
		FloatMat3 = ((int)0x8B5B),
		FloatMat4 = ((int)0x8B5C),
		Sampler2D = ((int)0x8B5E),
		Sampler3DOes = ((int)0x8B5F),
		SamplerCube = ((int)0x8B60),
		DeleteStatus = ((int)0x8B80),
		CompileStatus = ((int)0x8B81),
		LinkStatus = ((int)0x8B82),
		ValidateStatus = ((int)0x8B83),
		InfoLogLength = ((int)0x8B84),
		AttachedShaders = ((int)0x8B85),
		ActiveUniforms = ((int)0x8B86),
		ActiveUniformMaxLength = ((int)0x8B87),
		ShaderSourceLength = ((int)0x8B88),
		ActiveAttributes = ((int)0x8B89),
		ActiveAttributeMaxLength = ((int)0x8B8A),
		FragmentShaderDerivativeHintOes = ((int)0x8B8B),
		ShadingLanguageVersion = ((int)0x8B8C),
		CurrentProgram = ((int)0x8B8D),
		Palette4Rgb8Oes = ((int)0x8B90),
		Palette4Rgba8Oes = ((int)0x8B91),
		Palette4R5G6B5Oes = ((int)0x8B92),
		Palette4Rgba4Oes = ((int)0x8B93),
		Palette4Rgb5A1Oes = ((int)0x8B94),
		Palette8Rgb8Oes = ((int)0x8B95),
		Palette8Rgba8Oes = ((int)0x8B96),
		Palette8R5G6B5Oes = ((int)0x8B97),
		Palette8Rgba4Oes = ((int)0x8B98),
		Palette8Rgb5A1Oes = ((int)0x8B99),
		ImplementationColorReadType = ((int)0x8B9A),
		ImplementationColorReadFormat = ((int)0x8B9B),
		CounterTypeAmd = ((int)0x8BC0),
		CounterRangeAmd = ((int)0x8BC1),
		UnsignedInt64Amd = ((int)0x8BC2),
		PercentageAmd = ((int)0x8BC3),
		PerfmonResultAvailableAmd = ((int)0x8BC4),
		PerfmonResultSizeAmd = ((int)0x8BC5),
		PerfmonResultAmd = ((int)0x8BC6),
		CompressedRgbPvrtc4Bppv1Img = ((int)0x8C00),
		CompressedRgbPvrtc2Bppv1Img = ((int)0x8C01),
		CompressedRgbaPvrtc4Bppv1Img = ((int)0x8C02),
		CompressedRgbaPvrtc2Bppv1Img = ((int)0x8C03),
		CompressedRgbaPvrtc2Bppv2Img = ((int)0x9137),
		CompressedRgbaPvrtc4Bppv2Img = ((int)0x9138),
		CompressedRgbS3tcDxt1Ext = 0x83F0,
		CompressedSrgbS3tcDxt1Ext = 0x8C4C,
		CompressedRgbaS3tcDxt1Ext = 0x83F1,
		CompressedRgbaS3tcDxt3Ext = 0x83F2,
		CompressedRgbaS3tcDxt5Ext = 0x83F3,
		AtcRgbAmd = ((int)0x8C92),
		AtcRgbaExplicitAlphaAmd = ((int)0x8C93),
		StencilBackRef = ((int)0x8CA3),
		StencilBackValueMask = ((int)0x8CA4),
		StencilBackWritemask = ((int)0x8CA5),
		FramebufferBinding = ((int)0x8CA6),
		RenderbufferBinding = ((int)0x8CA7),
		FramebufferAttachmentObjectType = ((int)0x8CD0),
		FramebufferAttachmentObjectName = ((int)0x8CD1),
		FramebufferAttachmentTextureLevel = ((int)0x8CD2),
		FramebufferAttachmentTextureCubeMapFace = ((int)0x8CD3),
		FramebufferAttachmentTexture3DZoffsetOes = ((int)0x8CD4),
		FramebufferComplete = ((int)0x8CD5),
		FramebufferIncompleteAttachment = ((int)0x8CD6),
		FramebufferIncompleteMissingAttachment = ((int)0x8CD7),
		FramebufferIncompleteDimensions = ((int)0x8CD9),
		FramebufferUnsupported = ((int)0x8CDD),
		ColorAttachment0 = ((int)0x8CE0),
		DepthAttachment = ((int)0x8D00),
		StencilAttachment = ((int)0x8D20),
		Framebuffer = ((int)0x8D40),
		Renderbuffer = ((int)0x8D41),
		RenderbufferWidth = ((int)0x8D42),
		RenderbufferHeight = ((int)0x8D43),
		RenderbufferInternalFormat = ((int)0x8D44),
		StencilIndex1Oes = ((int)0x8D46),
		StencilIndex4Oes = ((int)0x8D47),
		StencilIndex8 = ((int)0x8D48),
		RenderbufferRedSize = ((int)0x8D50),
		RenderbufferGreenSize = ((int)0x8D51),
		RenderbufferBlueSize = ((int)0x8D52),
		RenderbufferAlphaSize = ((int)0x8D53),
		RenderbufferDepthSize = ((int)0x8D54),
		RenderbufferStencilSize = ((int)0x8D55),
		HalfFloatOes = ((int)0x8D61),
		Rgb565 = ((int)0x8D62),
		Etc1Rgb8Oes = ((int)0x8D64),
		TextureExternalOes = ((int)0x8D65),
		SamplerExternalOes = ((int)0x8D66),
		LowFloat = ((int)0x8DF0),
		MediumFloat = ((int)0x8DF1),
		HighFloat = ((int)0x8DF2),
		LowInt = ((int)0x8DF3),
		MediumInt = ((int)0x8DF4),
		HighInt = ((int)0x8DF5),
		UnsignedInt1010102Oes = ((int)0x8DF6),
		Int1010102Oes = ((int)0x8DF7),
		ShaderBinaryFormats = ((int)0x8DF8),
		NumShaderBinaryFormats = ((int)0x8DF9),
		ShaderCompiler = ((int)0x8DFA),
		MaxVertexUniformVectors = ((int)0x8DFB),
		MaxVaryingVectors = ((int)0x8DFC),
		MaxFragmentUniformVectors = ((int)0x8DFD),
		PerfmonGlobalModeQcom = ((int)0x8FA0),
		AmdCompressed3DcTexture = ((int)1),
		AmdCompressedAtcTexture = ((int)1),
		AmdPerformanceMonitor = ((int)1),
		AmdProgramBinaryZ400 = ((int)1),
		EsVersion20 = ((int)1),
		ExtTextureFilterAnisotropic = ((int)1),
		ExtTextureFormatBgra8888 = ((int)1),
		ExtTextureType2101010Rev = ((int)1),
		ImgReadFormat = ((int)1),
		ImgTextureCompressionPvrtc = ((int)1),
		NvFence = ((int)1),
		OesCompressedEtc1Rgb8Texture = ((int)1),
		OesCompressedPalettedTexture = ((int)1),
		OesDepth24 = ((int)1),
		OesDepth32 = ((int)1),
		OesDepthTexture = ((int)1),
		OesEglImage = ((int)1),
		OesElementIndexUint = ((int)1),
		OesFboRenderMipmap = ((int)1),
		OesFragmentPrecisionHigh = ((int)1),
		OesGetProgramBinary = ((int)1),
		OesMapbuffer = ((int)1),
		OesPackedDepthStencil = ((int)1),
		OesRgb8Rgba8 = ((int)1),
		OesStandardDerivatives = ((int)1),
		OesStencil1 = ((int)1),
		OesStencil4 = ((int)1),
		OesTexture3D = ((int)1),
		OesTextureFloat = ((int)1),
		OesTextureFloatLinear = ((int)1),
		OesTextureHalfFloat = ((int)1),
		OesTextureHalfFloatLinear = ((int)1),
		OesTextureNpot = ((int)1),
		OesVertexHalfFloat = ((int)1),
		OesVertexType1010102 = ((int)1),
		One = ((int)1),
		QcomDriverControl = ((int)1),
		QcomPerfmonGlobalMode = ((int)1),
		True = ((int)1),
	}

	public enum Amdcompressed3Dctexture : int
	{
		GL_3DcXAmd = ((int)0x87F9),
		GL_3DcXyAmd = ((int)0x87FA),
		AmdCompressed3DcTexture = ((int)1),
	}

	public enum AmdcompressedAtctexture : int
	{
		AtcRgbaInterpolatedAlphaAmd = ((int)0x87EE),
		AtcRgbAmd = ((int)0x8C92),
		AtcRgbaExplicitAlphaAmd = ((int)0x8C93),
		AmdCompressedAtcTexture = ((int)1),
	}

	public enum AmdperformanceMonitor : int
	{
		CounterTypeAmd = ((int)0x8BC0),
		CounterRangeAmd = ((int)0x8BC1),
		UnsignedInt64Amd = ((int)0x8BC2),
		PercentageAmd = ((int)0x8BC3),
		PerfmonResultAvailableAmd = ((int)0x8BC4),
		PerfmonResultSizeAmd = ((int)0x8BC5),
		PerfmonResultAmd = ((int)0x8BC6),
		AmdPerformanceMonitor = ((int)1),
	}

	public enum AmdprogramBinaryZ400 : int
	{
		Z400BinaryAmd = ((int)0x8740),
		AmdProgramBinaryZ400 = ((int)1),
	}

	public enum PrimitiveType : int
	{
		Points = ((int)0x0000),
		Lines = ((int)0x0001),
		LineLoop = ((int)0x0002),
		LineStrip = ((int)0x0003),
		Triangles = ((int)0x0004),
		TriangleStrip = ((int)0x0005),
		TriangleFan = ((int)0x0006),
	}

	public enum BlendEquationSeparate : int
	{
		FuncAdd = ((int)0x8006),
		BlendEquation = ((int)0x8009),
		BlendEquationAlpha = ((int)0x883D),
	}

	public enum BlendingFactorDest : int
	{
		Zero = ((int)0),
		SrcColor = ((int)0x0300),
		OneMinusSrcColor = ((int)0x0301),
		SrcAlpha = ((int)0x0302),
		OneMinusSrcAlpha = ((int)0x0303),
		DstAlpha = ((int)0x0304),
		OneMinusDstAlpha = ((int)0x0305),
		One = ((int)1),
	}

	public enum BlendingFactorSrc : int
	{
		DstColor = ((int)0x0306),
		OneMinusDstColor = ((int)0x0307),
		SrcAlphaSaturate = ((int)0x0308),
	}

	public enum BlendSubtract : int
	{
		FuncSubtract = ((int)0x800A),
		FuncReverseSubtract = ((int)0x800B),
	}

	public enum Boolean : int
	{
		False = ((int)0),
		True = ((int)1),
	}

	public enum BufferObjects : int
	{
		CurrentVertexAttrib = ((int)0x8626),
		BufferSize = ((int)0x8764),
		BufferUsage = ((int)0x8765),
		ArrayBuffer = ((int)0x8892),
		ElementArrayBuffer = ((int)0x8893),
		ArrayBufferBinding = ((int)0x8894),
		ElementArrayBufferBinding = ((int)0x8895),
		StreamDraw = ((int)0x88E0),
		StaticDraw = ((int)0x88E4),
		DynamicDraw = ((int)0x88E8),
	}

	[Flags]
	public enum ClearBufferMask : int
	{
		DepthBufferBit = ((int)0x00000100),
		StencilBufferBit = ((int)0x00000400),
		ColorBufferBit = ((int)0x00004000),
	}

	public enum CullFaceMode : int
	{
		Front = ((int)0x0404),
		Back = ((int)0x0405),
		FrontAndBack = ((int)0x0408),
	}

	public enum StencilFace : int
	{
		Front = ((int)0x0404),
		Back = ((int)0x0405),
		FrontAndBack = ((int)0x0408),
	}

	public enum DataType : int
	{
		Byte = ((int)0x1400),
		UnsignedByte = ((int)0x1401),
		Short = ((int)0x1402),
		UnsignedShort = ((int)0x1403),
		Int = ((int)0x1404),
		UnsignedInt = ((int)0x1405),
		Float = ((int)0x1406),
		Fixed = ((int)0x140C),
	}

	public enum EnableCap : int
	{
		CullFace = ((int)0x0B44),
		DepthTest = ((int)0x0B71),
		StencilTest = ((int)0x0B90),
		Dither = ((int)0x0BD0),
		Blend = ((int)0x0BE2),
		ScissorTest = ((int)0x0C11),
		Texture2D = ((int)0x0DE1),
		PolygonOffsetFill = ((int)0x8037),
		SampleAlphaToCoverage = ((int)0x809E),
		SampleCoverage = ((int)0x80A0),
		OpenGLUseMetalMGL = 0x6000,
		DebugModeMGL = 0x6001
	}

	public enum ErrorCode : int
	{
		NoError = ((int)0),
		InvalidEnum = ((int)0x0500),
		InvalidValue = ((int)0x0501),
		InvalidOperation = ((int)0x0502),
		OutOfMemory = ((int)0x0505),
	}

	public enum ExttextureFilterAnisotropic : int
	{
		TextureMaxAnisotropyExt = ((int)0x84FE),
		MaxTextureMaxAnisotropyExt = ((int)0x84FF),
		ExtTextureFilterAnisotropic = ((int)1),
	}

	public enum ExttextureFormatBgra8888 : int
	{
		Bgra = ((int)0x80E1),
		ExtTextureFormatBgra8888 = ((int)1),
	}

	public enum ExttextureType2101010Rev : int
	{
		UnsignedInt2101010RevExt = ((int)0x8368),
		ExtTextureType2101010Rev = ((int)1),
	}

	public enum FramebufferObject : int
	{
		None = ((int)0),
		InvalidFramebufferOperation = ((int)0x0506),
		StencilIndex = ((int)0x1901),
		Rgba4 = ((int)0x8056),
		Rgb5A1 = ((int)0x8057),
		DepthComponent16 = ((int)0x81A5),
		MaxRenderbufferSize = ((int)0x84E8),
		FramebufferBinding = ((int)0x8CA6),
		RenderbufferBinding = ((int)0x8CA7),
		FramebufferAttachmentObjectType = ((int)0x8CD0),
		FramebufferAttachmentObjectName = ((int)0x8CD1),
		FramebufferAttachmentTextureLevel = ((int)0x8CD2),
		FramebufferAttachmentTextureCubeMapFace = ((int)0x8CD3),
		FramebufferComplete = ((int)0x8CD5),
		FramebufferIncompleteAttachment = ((int)0x8CD6),
		FramebufferIncompleteMissingAttachment = ((int)0x8CD7),
		FramebufferIncompleteDimensions = ((int)0x8CD9),
		FramebufferUnsupported = ((int)0x8CDD),
		ColorAttachment0 = ((int)0x8CE0),
		DepthAttachment = ((int)0x8D00),
		StencilAttachment = ((int)0x8D20),
		Framebuffer = ((int)0x8D40),
		Renderbuffer = ((int)0x8D41),
		RenderbufferWidth = ((int)0x8D42),
		RenderbufferHeight = ((int)0x8D43),
		RenderbufferInternalFormat = ((int)0x8D44),
		StencilIndex8 = ((int)0x8D48),
		RenderbufferRedSize = ((int)0x8D50),
		RenderbufferGreenSize = ((int)0x8D51),
		RenderbufferBlueSize = ((int)0x8D52),
		RenderbufferAlphaSize = ((int)0x8D53),
		RenderbufferDepthSize = ((int)0x8D54),
		RenderbufferStencilSize = ((int)0x8D55),
		Rgb565 = ((int)0x8D62),
	}

	public enum FrontFaceDirection : int
	{
		Cw = ((int)0x0900),
		Ccw = ((int)0x0901),
	}

	public enum GetPName : int
	{
		LineWidth = ((int)0x0B21),
		CullFaceMode = ((int)0x0B45),
		FrontFace = ((int)0x0B46),
		DepthRange = ((int)0x0B70),
		DepthWritemask = ((int)0x0B72),
		DepthClearValue = ((int)0x0B73),
		DepthFunc = ((int)0x0B74),
		StencilClearValue = ((int)0x0B91),
		StencilFunc = ((int)0x0B92),
		StencilValueMask = ((int)0x0B93),
		StencilFail = ((int)0x0B94),
		StencilPassDepthFail = ((int)0x0B95),
		StencilPassDepthPass = ((int)0x0B96),
		StencilRef = ((int)0x0B97),
		StencilWritemask = ((int)0x0B98),
		Viewport = ((int)0x0BA2),
		ScissorBox = ((int)0x0C10),
		ColorClearValue = ((int)0x0C22),
		ColorWritemask = ((int)0x0C23),
		UnpackAlignment = ((int)0x0CF5),
		PackAlignment = ((int)0x0D05),
		MaxTextureSize = ((int)0x0D33),
		MaxViewportDims = ((int)0x0D3A),
		SubpixelBits = ((int)0x0D50),
		RedBits = ((int)0x0D52),
		GreenBits = ((int)0x0D53),
		BlueBits = ((int)0x0D54),
		AlphaBits = ((int)0x0D55),
		DepthBits = ((int)0x0D56),
		StencilBits = ((int)0x0D57),
		PolygonOffsetUnits = ((int)0x2A00),
		PolygonOffsetFactor = ((int)0x8038),
		TextureBinding2D = ((int)0x8069),
		SampleBuffers = ((int)0x80A8),
		Samples = ((int)0x80A9),
		SampleCoverageValue = ((int)0x80AA),
		SampleCoverageInvert = ((int)0x80AB),
		AliasedPointSizeRange = ((int)0x846D),
		AliasedLineWidthRange = ((int)0x846E),
		StencilBackFunc = ((int)0x8800),
		StencilBackFail = ((int)0x8801),
		StencilBackPassDepthFail = ((int)0x8802),
		StencilBackPassDepthPass = ((int)0x8803),
		StencilBackRef = ((int)0x8CA3),
		StencilBackValueMask = ((int)0x8CA4),
		StencilBackWritemask = ((int)0x8CA5),
		FramebufferBinding = ((int)0x8CA6),
		MaxTextureImageUnits = ((int)0x8872),
		MaxCombinedTextureImageUnits = ((int)0x8b4d),
		MaxVertexAttribs = ((int)0x8869),
	}

	public enum GetTextureParameter : int
	{
		NumCompressedTextureFormats = ((int)0x86A2),
		CompressedTextureFormats = ((int)0x86A3),
	}

	public enum HintMode : int
	{
		DontCare = ((int)0x1100),
		Fastest = ((int)0x1101),
		Nicest = ((int)0x1102),
	}

	public enum HintTarget : int
	{
		GenerateMipmapHint = ((int)0x8192),
	}

	public enum ImgreadFormat : int
	{
		Bgra = ((int)0x80E1),
		UnsignedShort4444Rev = ((int)0x8365),
		UnsignedShort1555Rev = ((int)0x8366),
		ImgReadFormat = ((int)1),
	}

	public enum ImgtextureCompressionPvrtc : int
	{
		CompressedRgbPvrtc4Bppv1Img = ((int)0x8C00),
		CompressedRgbPvrtc2Bppv1Img = ((int)0x8C01),
		CompressedRgbaPvrtc4Bppv1Img = ((int)0x8C02),
		CompressedRgbaPvrtc2Bppv1Img = ((int)0x8C03),
		ImgTextureCompressionPvrtc = ((int)1),
	}

	public enum Nvfence : int
	{
		AllCompletedNv = ((int)0x84F2),
		FenceStatusNv = ((int)0x84F3),
		FenceConditionNv = ((int)0x84F4),
		NvFence = ((int)1),
	}

	public enum OescompressedEtc1Rgb8Texture : int
	{
		Etc1Rgb8Oes = ((int)0x8D64),
		OesCompressedEtc1Rgb8Texture = ((int)1),
	}

	public enum OescompressedPalettedTexture : int
	{
		Palette4Rgb8Oes = ((int)0x8B90),
		Palette4Rgba8Oes = ((int)0x8B91),
		Palette4R5G6B5Oes = ((int)0x8B92),
		Palette4Rgba4Oes = ((int)0x8B93),
		Palette4Rgb5A1Oes = ((int)0x8B94),
		Palette8Rgb8Oes = ((int)0x8B95),
		Palette8Rgba8Oes = ((int)0x8B96),
		Palette8R5G6B5Oes = ((int)0x8B97),
		Palette8Rgba4Oes = ((int)0x8B98),
		Palette8Rgb5A1Oes = ((int)0x8B99),
		OesCompressedPalettedTexture = ((int)1),
	}

	public enum Oesdepth24 : int
	{
		DepthComponent24Oes = ((int)0x81A6),
		OesDepth24 = ((int)1),
	}

	public enum Oesdepth32 : int
	{
		DepthComponent32Oes = ((int)0x81A7),
		OesDepth32 = ((int)1),
	}

	public enum OesdepthTexture : int
	{
		OesDepthTexture = ((int)1),
	}

	public enum Oeseglimage : int
	{
		OesEglImage = ((int)1),
	}

	public enum OeselementIndexUint : int
	{
		OesElementIndexUint = ((int)1),
	}

	public enum OesfboRenderMipmap : int
	{
		OesFboRenderMipmap = ((int)1),
	}

	public enum OesfragmentPrecisionHigh : int
	{
		OesFragmentPrecisionHigh = ((int)1),
	}

	public enum OesgetProgramBinary : int
	{
		ProgramBinaryLengthOes = ((int)0x8741),
		NumProgramBinaryFormatsOes = ((int)0x87FE),
		ProgramBinaryFormatsOes = ((int)0x87FF),
		OesGetProgramBinary = ((int)1),
	}

	public enum Oesmapbuffer : int
	{
		WriteOnlyOes = ((int)0x88B9),
		BufferAccessOes = ((int)0x88BB),
		BufferMappedOes = ((int)0x88BC),
		BufferMapPointerOes = ((int)0x88BD),
		OesMapbuffer = ((int)1),
	}

	public enum OespackedDepthStencil : int
	{
		DepthStencilOes = ((int)0x84F9),
		UnsignedInt248Oes = ((int)0x84FA),
		Depth24Stencil8Oes = ((int)0x88F0),
		OesPackedDepthStencil = ((int)1),
	}

	public enum Oesrgb8Rgba8 : int
	{
		Rgb8Oes = ((int)0x8051),
		Rgba8Oes = ((int)0x8058),
		OesRgb8Rgba8 = ((int)1),
	}

	public enum OesstandardDerivatives : int
	{
		FragmentShaderDerivativeHintOes = ((int)0x8B8B),
		OesStandardDerivatives = ((int)1),
	}

	public enum Oesstencil1 : int
	{
		StencilIndex1Oes = ((int)0x8D46),
		OesStencil1 = ((int)1),
	}

	public enum Oesstencil4 : int
	{
		StencilIndex4Oes = ((int)0x8D47),
		OesStencil4 = ((int)1),
	}

	public enum Oestexture3D : int
	{
		TextureBinding3DOes = ((int)0x806A),
		Texture3DOes = ((int)0x806F),
		TextureWrapROes = ((int)0x8072),
		Max3DTextureSizeOes = ((int)0x8073),
		Sampler3DOes = ((int)0x8B5F),
		FramebufferAttachmentTexture3DZoffsetOes = ((int)0x8CD4),
		OesTexture3D = ((int)1),
	}

	public enum OestextureFloat : int
	{
		OesTextureFloat = ((int)1),
	}

	public enum OestextureFloatLinear : int
	{
		OesTextureFloatLinear = ((int)1),
	}

	public enum OestextureHalfFloat : int
	{
		HalfFloatOes = ((int)0x8D61),
		OesTextureHalfFloat = ((int)1),
	}

	public enum OestextureHalfFloatLinear : int
	{
		OesTextureHalfFloatLinear = ((int)1),
	}

	public enum OestextureNpot : int
	{
		OesTextureNpot = ((int)1),
	}

	public enum OesvertexHalfFloat : int
	{
		OesVertexHalfFloat = ((int)1),
	}

	public enum OesvertexType1010102 : int
	{
		UnsignedInt1010102Oes = ((int)0x8DF6),
		Int1010102Oes = ((int)0x8DF7),
		OesVertexType1010102 = ((int)1),
	}

	public enum OpenGlescoreVersions : int
	{
		EsVersion20 = ((int)1),
	}

	public enum PixelFormat : int
	{
		DepthComponent = ((int)0x1902),
		Alpha = ((int)0x1906),
		Rgb = ((int)0x1907),
		Rgba = ((int)0x1908),
		Luminance = ((int)0x1909),
		LuminanceAlpha = ((int)0x190A),
	}

	public enum PixelType : int
	{
		UnsignedByte = 5121,
		UnsignedShort4444 = ((int)0x8033),
		UnsignedShort5551 = ((int)0x8034),
		UnsignedShort565 = ((int)0x8363),
	}

	public enum QcomdriverControl : int
	{
		QcomDriverControl = ((int)1),
	}

	public enum QcomperfmonGlobalMode : int
	{
		PerfmonGlobalModeQcom = ((int)0x8FA0),
		QcomPerfmonGlobalMode = ((int)1),
	}

	public enum ReadFormat : int
	{
		ImplementationColorReadType = ((int)0x8B9A),
		ImplementationColorReadFormat = ((int)0x8B9B),
	}

	public enum SeparateBlendFunctions : int
	{
		ConstantColor = ((int)0x8001),
		OneMinusConstantColor = ((int)0x8002),
		ConstantAlpha = ((int)0x8003),
		OneMinusConstantAlpha = ((int)0x8004),
		BlendColor = ((int)0x8005),
		BlendDstRgb = ((int)0x80C8),
		BlendSrcRgb = ((int)0x80C9),
		BlendDstAlpha = ((int)0x80CA),
		BlendSrcAlpha = ((int)0x80CB),
	}

	public enum ShaderBinary : int
	{
		ShaderBinaryFormats = ((int)0x8DF8),
		NumShaderBinaryFormats = ((int)0x8DF9),
	}

	public enum ShaderPrecisionSpecifiedTypes : int
	{
		LowFloat = ((int)0x8DF0),
		MediumFloat = ((int)0x8DF1),
		HighFloat = ((int)0x8DF2),
		LowInt = ((int)0x8DF3),
		MediumInt = ((int)0x8DF4),
		HighInt = ((int)0x8DF5),
	}

	public enum Shaders : int
	{
		MaxVertexAttribs = ((int)0x8869),
		MaxTextureImageUnits = ((int)0x8872),
		FragmentShader = ((int)0x8B30),
		VertexShader = ((int)0x8B31),
		MaxVertexTextureImageUnits = ((int)0x8B4C),
		MaxCombinedTextureImageUnits = ((int)0x8B4D),
		ShaderType = ((int)0x8B4F),
		DeleteStatus = ((int)0x8B80),
		LinkStatus = ((int)0x8B82),
		ValidateStatus = ((int)0x8B83),
		AttachedShaders = ((int)0x8B85),
		ActiveUniforms = ((int)0x8B86),
		ActiveUniformMaxLength = ((int)0x8B87),
		ActiveAttributes = ((int)0x8B89),
		ActiveAttributeMaxLength = ((int)0x8B8A),
		ShadingLanguageVersion = ((int)0x8B8C),
		CurrentProgram = ((int)0x8B8D),
		MaxVertexUniformVectors = ((int)0x8DFB),
		MaxVaryingVectors = ((int)0x8DFC),
		MaxFragmentUniformVectors = ((int)0x8DFD),
	}

	public enum ShaderSource : int
	{
		CompileStatus = ((int)0x8B81),
		InfoLogLength = ((int)0x8B84),
		ShaderSourceLength = ((int)0x8B88),
		ShaderCompiler = ((int)0x8DFA),
	}

	public enum StencilFunction : int
	{
		Never = ((int)0x0200),
		Less = ((int)0x0201),
		Equal = ((int)0x0202),
		Lequal = ((int)0x0203),
		Greater = ((int)0x0204),
		Notequal = ((int)0x0205),
		Gequal = ((int)0x0206),
		Always = ((int)0x0207),
	}

	public enum StencilOp : int
	{
		Zero,
		Invert = ((int)0x150A),
		Keep = ((int)0x1E00),
		Replace = ((int)0x1E01),
		Incr = ((int)0x1E02),
		Decr = ((int)0x1E03),
		IncrWrap = ((int)0x8507),
		DecrWrap = ((int)0x8508),
	}

	public enum StringName : int
	{
		Vendor = ((int)0x1F00),
		Renderer = ((int)0x1F01),
		Version = ((int)0x1F02),
		Extensions = ((int)0x1F03),
	}

	public enum TextureMagFilter : int
	{
		Nearest = ((int)0x2600),
		Linear = ((int)0x2601),
	}

	public enum TextureMinFilter : int
	{
		NearestMipmapNearest = ((int)0x2700),
		LinearMipmapNearest = ((int)0x2701),
		NearestMipmapLinear = ((int)0x2702),
		LinearMipmapLinear = ((int)0x2703),
	}

	public enum TextureParameterName : int
	{
		TextureMagFilter = ((int)0x2800),
		TextureMinFilter = ((int)0x2801),
		TextureWrapS = ((int)0x2802),
		TextureWrapT = ((int)0x2803),
	}

	public enum TextureTarget : int
	{
		Texture = ((int)0x1702),
		TextureCubeMap = ((int)0x8513),
		TextureBindingCubeMap = ((int)0x8514),
		TextureCubeMapPositiveX = ((int)0x8515),
		TextureCubeMapNegativeX = ((int)0x8516),
		TextureCubeMapPositiveY = ((int)0x8517),
		TextureCubeMapNegativeY = ((int)0x8518),
		TextureCubeMapPositiveZ = ((int)0x8519),
		TextureCubeMapNegativeZ = ((int)0x851A),
		MaxCubeMapTextureSize = ((int)0x851C),
		Texture2D = ((int)0x0DE1),
	}

	public enum TextureUnit : int
	{
		Texture0 = ((int)0x84C0),
		Texture1 = ((int)0x84C1),
		Texture2 = ((int)0x84C2),
		Texture3 = ((int)0x84C3),
		Texture4 = ((int)0x84C4),
		Texture5 = ((int)0x84C5),
		Texture6 = ((int)0x84C6),
		Texture7 = ((int)0x84C7),
		Texture8 = ((int)0x84C8),
		Texture9 = ((int)0x84C9),
		Texture10 = ((int)0x84CA),
		Texture11 = ((int)0x84CB),
		Texture12 = ((int)0x84CC),
		Texture13 = ((int)0x84CD),
		Texture14 = ((int)0x84CE),
		Texture15 = ((int)0x84CF),
		Texture16 = ((int)0x84D0),
		Texture17 = ((int)0x84D1),
		Texture18 = ((int)0x84D2),
		Texture19 = ((int)0x84D3),
		Texture20 = ((int)0x84D4),
		Texture21 = ((int)0x84D5),
		Texture22 = ((int)0x84D6),
		Texture23 = ((int)0x84D7),
		Texture24 = ((int)0x84D8),
		Texture25 = ((int)0x84D9),
		Texture26 = ((int)0x84DA),
		Texture27 = ((int)0x84DB),
		Texture28 = ((int)0x84DC),
		Texture29 = ((int)0x84DD),
		Texture30 = ((int)0x84DE),
		Texture31 = ((int)0x84DF),
		ActiveTexture = ((int)0x84E0),
	}

	public enum TextureWrapMode : int
	{
		Repeat = ((int)0x2901),
		Clamp = ((int)0x812F),
		MirroredRepeat = ((int)0x8370),
	}

	public enum UniformTypes : int
	{
		FloatVec2 = ((int)0x8B50),
		FloatVec3 = ((int)0x8B51),
		FloatVec4 = ((int)0x8B52),
		IntVec2 = ((int)0x8B53),
		IntVec3 = ((int)0x8B54),
		IntVec4 = ((int)0x8B55),
		Bool = ((int)0x8B56),
		BoolVec2 = ((int)0x8B57),
		BoolVec3 = ((int)0x8B58),
		BoolVec4 = ((int)0x8B59),
		FloatMat2 = ((int)0x8B5A),
		FloatMat3 = ((int)0x8B5B),
		FloatMat4 = ((int)0x8B5C),
		Sampler2D = ((int)0x8B5E),
		SamplerCube = ((int)0x8B60),
	}

	public enum VertexArrays : int
	{
		VertexAttribArrayEnabled = ((int)0x8622),
		VertexAttribArraySize = ((int)0x8623),
		VertexAttribArrayStride = ((int)0x8624),
		VertexAttribArrayType = ((int)0x8625),
		VertexAttribArrayPointer = ((int)0x8645),
		VertexAttribArrayNormalized = ((int)0x886A),
		VertexAttribArrayBufferBinding = ((int)0x889F),
	}

	public enum RenderbufferTarget : int
	{
		Renderbuffer = 36161
	}

	public enum BufferTarget : int
	{
		ArrayBuffer = 34962,
		ElementArrayBuffer
	}

	public enum BufferUsageHint : int
	{
		StreamDraw = ((int)0x88e0),
		StreamRead = ((int)0x88e1),
		StreamCopy = ((int)0x88e2),
		StaticDraw = ((int)0x88e4),
		StaticRead = ((int)0x88e5),
		StaticCopy = ((int)0x88e6),
		DynamicDraw = ((int)0x88e8),
		DynamicRead = ((int)0x88e9),
		DynamicCopy = ((int)0x88ea),
	}

	public enum FramebufferTarget : int
	{
		Framebuffer = 36160
	}

	public enum BlendEquationMode : int
	{
		FuncAdd = 32774,
		FuncSubtract = 32778,
		FuncReverseSubtract
	}

	public enum FramebufferErrorCode : int
	{
		FramebufferComplete = 36053,
		FramebufferIncompleteAttachment,
		FramebufferIncompleteMissingAttachment,
		FramebufferIncompleteDimensions = 36057,
		FramebufferUnsupported = 36061
	}

	public enum PixelInternalFormat : int
	{
		Alpha = 6406,
		Rgb,
		Rgba,
		Luminance,
		LuminanceAlpha,
		CompressedRgbPvrtc4Bppv1Img = ((int)0x8C00),
		CompressedRgbPvrtc2Bppv1Img = ((int)0x8C01),
		CompressedRgbaPvrtc4Bppv1Img = ((int)0x8C02),
		CompressedRgbaPvrtc2Bppv1Img = ((int)0x8C03),
	}

	public enum ShaderType : int
	{
		FragmentShader = 35632,
		VertexShader
	}

	public enum DepthFunction : int
	{
		Never = 0x200,
		Less,
		Equal,
		Lequal,
		Greater,
		Notequal,
		Gequal,
		Always
	}

	public enum DrawElementsType : int
	{
		UnsignedByte = 5121,
		UnsignedShort = 5123,
		UnsignedInt = 5125
	}

	public enum FramebufferSlot : int
	{
		ColorAttachment0 = 36064,
		DepthAttachment = 36096,
		StencilAttachment = 36128
	}

	public enum ActiveAttribType : int
	{
		Float = 5126,
		FloatVec2 = 35664,
		FloatVec3,
		FloatVec4,
		FloatMat2 = 35674,
		FloatMat3,
		FloatMat4
	}

	public enum ActiveUniformType : int
	{
		Int = 5124,
		Float = 5126,
		FloatVec2 = 35664,
		FloatVec3,
		FloatVec4,
		IntVec2,
		IntVec3,
		IntVec4,
		Bool,
		BoolVec2,
		BoolVec3,
		BoolVec4,
		FloatMat2,
		FloatMat3,
		FloatMat4,
		Sampler2D = 35678,
		SamplerCube = 35680
	}

	public enum BufferParameterName : int
	{
		BufferSize = 34660,
		BufferUsage
	}

	public enum FramebufferParameterName : int
	{
		FramebufferAttachmentObjectType = 36048,
		FramebufferAttachmentObjectName,
		FramebufferAttachmentTextureLevel,
		FramebufferAttachmentTextureCubeMapFace
	}

	public enum ProgramParameter : int
	{
		DeleteStatus = 35712,
		LinkStatus = 35714,
		ValidateStatus,
		InfoLogLength,
		AttachedShaders,
		ActiveUniforms,
		ActiveUniformMaxLength,
		ActiveAttributes = 35721,
		ActiveAttributeMaxLength
	}

	public enum RenderbufferParameterName : int
	{
		RenderbufferWidth = 36162,
		RenderbufferHeight,
		RenderbufferInternalFormat,
		RenderbufferRedSize = 36176,
		RenderbufferGreenSize,
		RenderbufferBlueSize,
		RenderbufferAlphaSize,
		RenderbufferDepthSize,
		RenderbufferStencilSize
	}

	public enum ShaderParameter : int
	{
		ShaderType = 35663,
		DeleteStatus = 35712,
		CompileStatus,
		InfoLogLength = 35716,
		ShaderSourceLength = 35720
	}

	public enum ShaderPrecision : int
	{
		LowFloat = 36336,
		MediumFloat,
		HighFloat,
		LowInt,
		MediumInt,
		HighInt
	}

	public enum VertexAttribParameter : int
	{
		VertexAttribArrayEnabled = 34338,
		VertexAttribArraySize,
		VertexAttribArrayStride,
		VertexAttribArrayType,
		CurrentVertexAttrib,
		VertexAttribArrayNormalized = 34922,
		VertexAttribArrayBufferBinding = 34975
	}

	public enum VertexAttribPointerParameter : int
	{
		VertexAttribArrayPointer = 34373
	}

	public enum PixelStoreParameter : int
	{
		UnpackAlignment = 3317,
		PackAlignment = 3333
	}

	public enum RenderbufferInternalFormat : int
	{
		Rgba4 = 32854,
		Rgb5A1,
		DepthComponent16 = 33189,
		StencilIndex8 = 36168,
		Rgb565 = 36194
	}

	public enum VertexAttribPointerType : int
	{
		Byte = 5120,
		UnsignedByte,
		Short,
		UnsignedShort,
		Float = 5126,
		Fixed = 5132
	}
}
#endif
