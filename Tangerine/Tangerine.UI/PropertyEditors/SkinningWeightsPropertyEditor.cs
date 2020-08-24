using System;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class SkinningWeightsPropertyEditor : ExpandablePropertyEditor<SkinningWeights>
	{
		private readonly NumericEditBox[] indexEditors;
		private readonly ThemedAreaSlider[] weightsSliders;
		private readonly Widget customWarningsContainer;

		private float previousValue;

		public SkinningWeightsPropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			editorParams.DefaultValueGetter = () => new SkinningWeights();
			indexEditors = new NumericEditBox[4];
			weightsSliders = new ThemedAreaSlider[4];
			foreach (var o in editorParams.Objects) {
				var prop = new Property<SkinningWeights>(o, editorParams.PropertyName).Value;
			}
			for (var i = 0; i <= 3; i++) {
				indexEditors[i] = editorParams.NumericEditBoxFactory();
				indexEditors[i].Step = 1;
				weightsSliders[i] = new ThemedAreaSlider(range: new Vector2(0, 1), labelFormat: "0.00000");
				var wrapper = new Widget {
					Layout = new HBoxLayout(),
					LayoutCell = new LayoutCell { StretchY = 0 }
				};
				var propertyLabel = new ThemedSimpleText {
					Text = $"Bone { char.ConvertFromUtf32(65 + i) }",
					VAlignment = VAlignment.Center,
					Padding = new Thickness { Left = 20 },
					LayoutCell = new LayoutCell { StretchX = 1.0f },
					ForceUncutText = false,
					OverflowMode = TextOverflowMode.Minify,
					HitTestTarget = false
				};
				wrapper.AddNode(propertyLabel);
				wrapper.AddNode(new Widget {
					Layout = new HBoxLayout { Spacing = 4 },
					LayoutCell = new LayoutCell { StretchX = 2.0f },
					Nodes = {
						indexEditors[i] ,
						weightsSliders[i]
					}
				});
				ExpandableContent.AddNode(wrapper);
				customWarningsContainer = new Widget {
					Layout = new VBoxLayout()
				};
				ContainerWidget.AddNode(customWarningsContainer);
				var j = i;
				SetLink(i, CoalescedPropertyComponentValue(sw => sw[j].Index), CoalescedPropertyComponentValue(sw => sw[j].Weight));
			}
			CheckWarnings();
		}

		private void SetLink(int idx, IDataflowProvider<CoalescedValue<int>> indexProvider, IDataflowProvider<CoalescedValue<float>> weightProvider)
		{
			var currentIndexValue = indexProvider.GetValue();
			var currentWeightValue = weightProvider.GetValue();
			indexEditors[idx].Submitted += text => SetIndexValue(idx, indexEditors[idx], currentIndexValue);
			weightsSliders[idx].Changed += () => SetWeightValue(idx, weightsSliders[idx]);
			weightsSliders[idx].Value = currentWeightValue.IsDefined ? currentWeightValue.Value : 0;
			indexEditors[idx].AddChangeLateWatcher(indexProvider,
				v => indexEditors[idx].Text = v.IsDefined ? v.Value.ToString() : ManyValuesText);
			weightsSliders[idx].AddChangeLateWatcher(weightProvider,
				v => {
					weightsSliders[idx].Value = v.IsDefined ? v.Value : 0;
					if (!v.IsDefined) {
						weightsSliders[idx].LabelText = ManyValuesText;
					}
				});
			weightsSliders[idx].DragStarted += () => {
				EditorParams.History?.BeginTransaction();
				previousValue = weightsSliders[idx].Value;
			};
			weightsSliders[idx].DragEnded += () => {
				if (weightsSliders[idx].Value != previousValue || (EditorParams.Objects.Skip(1).Any() && SameValues())) {
					EditorParams.History?.CommitTransaction();
				}
				EditorParams.History?.EndTransaction();
			};
			ManageManyValuesOnFocusChange(indexEditors[idx], indexProvider);
			ManageManyValuesOnFocusChange(weightsSliders[idx].Editor, weightProvider);
		}

		private void SetIndexValue(int idx, CommonEditBox editor, CoalescedValue<int> prevValue)
		{
			if (float.TryParse(editor.Text, out float newValue)) {
				DoTransaction(() => {
					SetProperty<SkinningWeights>((current) => {
						current[idx] = new BoneWeight {
							Index = (int)newValue,
							Weight = current[idx].Weight
						};
						CheckWarnings();
						return current;
					});
				});
			} else {
				editor.Text = prevValue.IsDefined ? prevValue.Value.ToString() : ManyValuesText;
			}
		}

		private void SetWeightValue(int idx, ThemedAreaSlider slider)
		{
			DoTransaction(() => {
				SetProperty<SkinningWeights>((current) => {
					CheckWarnings();
					current[idx] = new BoneWeight {
						Index = current[idx].Index,
						Weight = slider.Value
					};
					return current;
				});
			});
		}

		private void CheckWarnings()
		{
			bool IsBoneWeightValid(float weight) => weight == Mathf.Clamp(weight, 0, 1);
			bool isOutOfRange = false;
			foreach (var slider in weightsSliders) {
				isOutOfRange |= !IsBoneWeightValid(slider.Value);
			}
			if (isOutOfRange) {
				if (customWarningsContainer.Nodes.Count == 0) {
					customWarningsContainer.AddNode(
						CommonPropertyEditor.CreateWarning(
							message: "Bone weight should be in the range [0, 1].",
							validationResult: ValidationResult.Warning));
				}
			} else {
				customWarningsContainer.Nodes.Clear();
			}
		}
	}
}
