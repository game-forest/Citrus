# Tangerine font configuration

Tangerine Font Configuration (tftconf) is an asset describing how to create Tangerine Font asset (tft).

The format is JSON:

Property | Type | Description
---------|------|------------
`Height` |float|Font's point size that will be used to generate the font's texture.
`Padding`|int|Characters in the font texture need some padding between them so they can be rendered separately. This padding is specified in pixels.Padding also creates room for the SDF gradient. The larger the padding, the smoother the transition, which allows for higher-quality rendering and larger effects, like thicker outlines.
`CharSets`|[CharSet](#charset)|That's the main part of configuration file that describes what font files and symbols should be used for Tangerine Font creation.
`ExcludeChars`|string|Characters that will be ignored during the font generation step.
`IsSdf`|bool|If it's `true` Signed Distance Font (SDF) will be generated otherwise simple bitmap font.
`SdfScale`|float|When using an SFD font, a higher resolution results in finer gradients, which produces higher quality text. That's why it's a common way to sample characters at high resolution to create good gradients and then downscale. For example you can generated 8k textures with bigger font size and then downscale them to 4k with `SdfScale = 0.5f`.
`TextureSize` |[Vector2](#vector2)|Size for each texture **before** downscale.
`CustomKerningPairs`|[CustomKerningPairs](#CustomKerningPairs)|Kerning pairs are used to adjust the spacing between specific character pairs, to produce a more visually pleasing result. Note that many fonts do not have kerning pairs or may not satisfy your needs so you can simply add your own pairs or override ones taken from the font file.
`Margin`|float|**Obsolete**. Expands AC width. Was used for workarounds.

## `CharSet`

Property | Type | Description
---------|------|------------
`Chars`|string|A list of characters to be used for font generation. Can be `null` or empty (see `ExtractFromDictionaries`).
`Font`|string|Path (relative to asset directory) to font file from which glyphs will be taken. See [supported font formats](#Supported-formats).
`ExtractFromDictionaries`|string|A comma separated set of localizations (e.g. "EN,RU,CN"). If it's null or empty existing Chars are used, otherwise Chars are extracted from localization dictionaries (e.g. "Dictionary.RU.txt" or "Dictionary.txt" for EN).

## `Vector2`

array of two numbers (YuzuCompact).

```json
[12, 34]
```
## `CustomKerningPairs`

It's a dictionary with character key and array of [KerningPair](#KerningPair) values. Each kerning pair in array represents kerning amount that should be applied if character follows array's key of the dictionary. For example this:

```json
CustomKerningPairs: { 'A': [['V', -2]] }
```

means that position of `V` should be decreased by 2 if it follows `A`.


## `KerningPair`

Array of two elements: char and float (YuzuCompact).

```json
['V', -3]
```

## Supported formats

Supported font formats are those that are supported by FreeType:

- TrueType files (.ttf) and collections (.ttc)
- Type 1 font files both in ASCII (.pfa) or binary (.pfb) format
- Type 1 Multiple Master fonts
- Type 1 CID-keyed fonts
- OpenType/CFF (.otf) font
- CFF/Type 2 fonts
- Adobe CEF fonts (.cef), used to embed fonts in SVG documents with the Adobe SVG viewer plugin
- Windows FNT/FON bitmap fonts
- Apple's TrueType GX fonts are supported as normal TTFs (the advanced tables are ignored)

# How to generate font

- Place a font file inside `Fonts` directory (e.g. Digits.ttf)
- Place a configuration file inside `Fonts` directory. Example of .tftconf to generate SDF font with digits only:
```json
{
    "CharSets":[
		{
			"Chars":"0123456789",
			"Font":"Fonts/Digits.ttf"
		}
	],
	"Height":170,
	"IsSdf":true,
    "SdfScale": 0.25,
	"Padding":36,
	"TextureSize":[4096,4096]
}
```
- Run `Invalidate Fonts` action via Orange or Tangerine (Orange -> Invalidate Fonts)
- You'll be notified after engine finishes font generation. You should be able to see `Digits.ttf` and `Digit.png` files in `Fonts` directory
- Unfortunately you have to restart Tangerine for changes to take place :(
