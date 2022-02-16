using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Lime;
using System.Reflection;

namespace Tangerine.Core.Operations
{
	public sealed class DelegateOperation : Operation
	{
		private readonly bool isChangingDocument;
		public override bool IsChangingDocument => isChangingDocument;
		public readonly Action Redo;
		public readonly Action Undo;

		public static void Perform(Action redo, Action undo, bool isChangingDocument)
		{
			DocumentHistory.Current.Perform(new DelegateOperation(redo, undo, isChangingDocument));
		}

		private DelegateOperation(Action redo, Action undo, bool isChangingDocument)
		{
			this.Redo = redo;
			this.Undo = undo;
			this.isChangingDocument = isChangingDocument;
		}

		public sealed class Processor : OperationProcessor<DelegateOperation>
		{
			protected override void InternalRedo(DelegateOperation op) => op.Redo?.Invoke();

			protected override void InternalUndo(DelegateOperation op) => op.Undo?.Invoke();
		}
	}

	public class SetProperty : Operation
	{
		public readonly object Obj;
		public readonly object Value;
		public readonly PropertyInfo Property;
		public override bool IsChangingDocument { get; }

		public static void Perform(object obj, string propertyName, object value, bool isChangingDocument = true)
		{
			DocumentHistory.Current.Perform(new SetProperty(obj, propertyName, value, isChangingDocument));
		}

		protected SetProperty(object obj, string propertyName, object value, bool isChangingDocument)
		{
			Obj = obj;
			Value = value;
			Property = obj.GetType().GetProperty(propertyName);
			IsChangingDocument = isChangingDocument;
		}

		public sealed class Processor : OperationProcessor<SetProperty>
		{
			private class Backup { public object Value; }

			protected override void InternalRedo(SetProperty op)
			{
				op.Save(new Backup { Value = op.Property.GetValue(op.Obj, null) });
				op.Property.SetValue(op.Obj, op.Value, null);
				PropertyAttributes<TangerineOnPropertySetAttribute>.Get(op.Obj.GetType(), op.Property.Name)?.Invoke(op.Obj);
			}

			protected override void InternalUndo(SetProperty op)
			{
				var v = op.Restore<Backup>().Value;
				op.Property.SetValue(op.Obj, v, null);
				PropertyAttributes<TangerineOnPropertySetAttribute>.Get(op.Obj.GetType(), op.Property.Name)?.Invoke(op.Obj);
			}
		}
	}

	public sealed class SetIndexedProperty : Operation
	{
		public readonly object Obj;
		public readonly object Value;
		public readonly int Index;
		public readonly PropertyInfo Property;
		public readonly Type Type;
		public override bool IsChangingDocument { get; }

		public static void Perform(object obj, string propertyName, int index, object value, bool isChangingDocument = true)
		{
			DocumentHistory.Current.Perform(new SetIndexedProperty(obj, propertyName, index, value, isChangingDocument));
		}

		public static void Perform(Type type, object obj, string propertyName, int indexProvider, object value, bool isChangingDocument = true)
		{
			DocumentHistory.Current.Perform(new SetIndexedProperty(type, obj, propertyName, indexProvider, value, isChangingDocument));
		}

		private SetIndexedProperty(object obj, string propertyName, int index, object value, bool isChangingDocument)
		{
			Type = obj.GetType();
			Obj = obj;
			Index = index;
			Value = value;
			Property = Type.GetProperty(propertyName);
			IsChangingDocument = isChangingDocument;
		}

		private SetIndexedProperty(Type type, object obj, string propertyName, int index, object value, bool isChangingDocument)
		{
			Type = type;
			Obj = obj;
			Index = index;
			Value = value;
			Property = Type.GetProperty(propertyName);
			IsChangingDocument = isChangingDocument;
		}

		public sealed class Processor : OperationProcessor<SetIndexedProperty>
		{
			private class Backup { public object Value; }

			protected override void InternalRedo(SetIndexedProperty op)
			{
				op.Save(new Backup { Value = op.Property.GetGetMethod().Invoke(op.Obj, new object[] { op.Index }) });
				op.Property.GetSetMethod().Invoke(op.Obj, new [] { op.Index, op.Value });
			}

			protected override void InternalUndo(SetIndexedProperty op)
			{
				var v = op.Restore<Backup>().Value;
				op.Property.GetSetMethod().Invoke(op.Obj, new[] { op.Index, v });
			}
		}
	}

	public static class SetAnimableProperty
	{
		public static void Perform(object @object, string propertyPath, object value, bool createAnimatorIfNeeded = false, bool createInitialKeyframeForNewAnimator = true, int atFrame = -1)
		{
			var animationHost = @object as IAnimationHost;
			object owner = @object;
			int index = -1;
			var propertyData = AnimationUtils.PropertyData.Empty;
			if (animationHost != null) {
				(propertyData, owner, index) = AnimationUtils.GetPropertyByPath(animationHost, propertyPath);
			}
			// Discard further work if the property is not editable and subject for inspection.
			var tangerineIgnoreIf = PropertyAttributes<TangerineIgnoreIfAttribute>.Get(propertyData.OwnerType, propertyData.Info.Name);
			if (tangerineIgnoreIf?.Check(owner) ?? false) {
				return;
			}
			if (animationHost is Node && animationHost.Animators.TryFind(propertyPath, out var zeroPoseAnimator, Animation.ZeroPoseId)) {
				// Force create a property animator if there is a zero pose animator
				createAnimatorIfNeeded = true;
			}
			if (
				animationHost != null &&
				SetKeyframe.CheckAnimationScope(Document.Current.Animation, animationHost) &&
				(animationHost.Animators.TryFind(propertyPath, out var animator, Document.Current.AnimationId) || createAnimatorIfNeeded)
			) {
				if (animator == null && createInitialKeyframeForNewAnimator) {
					var propertyValue = propertyData.Info.GetValue(owner);
					Perform(animationHost, propertyPath, propertyValue, true, false, 0);
				}
				var type = propertyData.Info.PropertyType;
				var key =
					animator?.ReadonlyKeys.GetByFrame(Document.Current.AnimationFrame)?.Clone() ??
					Keyframe.CreateForType(type);
				key.Frame = atFrame == -1 ? Document.Current.AnimationFrame : atFrame;
				key.Function = animator?.Keys.LastOrDefault(k => k.Frame <= key.Frame)?.Function ?? KeyFunction.Linear;
				key.Value = value;
				SetKeyframe.Perform(animationHost, propertyPath, Document.Current.Animation, key);
			}
			// Set property after setting a keyframe, since SetKeyframe may store the current value as a zero pose value
			if (index == -1) {
				SetProperty.Perform(owner, propertyData.Info?.Name ?? propertyPath, value);
			} else {
				SetIndexedProperty.Perform(owner, propertyData.Info?.Name ?? propertyPath, index, value);
			}
		}
	}

	public static class ProcessAnimableProperty
	{

		public delegate bool AnimablePropertyProcessor<T>(T value, out T newValue);

		public static void Perform<T>(object @object, string propertyPath, AnimablePropertyProcessor<T> propertyProcessor)
		{
			var propertyInfo = @object.GetType().GetProperty(propertyPath);
			if (propertyInfo != null) {
				var value = propertyInfo.GetValue(@object);
				if (value is T) {
					T processedValue;
					if (propertyProcessor((T) value, out processedValue)) {
						SetProperty.Perform(@object, propertyPath, processedValue);
					}
				}
			}

			IAnimator animator;
			var animable = @object as IAnimationHost;
			if (animable != null && animable.Animators.TryFind(propertyPath, out animator, Document.Current.AnimationId)) {
				foreach (var keyframe in animator.ReadonlyKeys.ToList()) {
					if (!(keyframe.Value is T)) continue;

					T processedValue;
					if (propertyProcessor((T) keyframe.Value, out processedValue)) {
						var keyframeClone = keyframe.Clone();
						keyframeClone.Value = processedValue;
						SetKeyframe.Perform(animator, Document.Current.Animation, keyframeClone);
					}
				}
			}
		}
	}

	public sealed class RemoveKeyframe : Operation
	{
		public readonly int Frame;
		public readonly IAnimator Animator;
		public readonly IAnimationHost AnimationHost;
		public readonly bool RemoveEmptyAnimator;

		public override bool IsChangingDocument => true;

		public static void Perform(IAnimator animator, int frame, bool removeEmptyAnimator = true)
		{
			DocumentHistory.Current.Perform(new RemoveKeyframe(animator, frame, removeEmptyAnimator));
		}

		private RemoveKeyframe(IAnimator animator, int frame, bool removeEmptyAnimator)
		{
			Frame = frame;
			Animator = animator;
			AnimationHost = Animator.Owner;
			RemoveEmptyAnimator = removeEmptyAnimator;
		}

		public sealed class Processor : OperationProcessor<RemoveKeyframe>
		{
			class Backup { public IKeyframe Keyframe; }

			protected override void InternalRedo(RemoveKeyframe op)
			{
				var kf = op.Animator.Keys.GetByFrame(op.Frame);
				op.Save(new Backup { Keyframe = kf });
				op.Animator.Keys.Remove(kf);
				if (op.RemoveEmptyAnimator && op.Animator.Keys.Count == 0) {
					op.AnimationHost.Animators.Remove(op.Animator);
					Document.Current.RefreshSceneTree();
				} else {
					op.Animator.ResetCache();
				}
				if (op.Animator.TargetPropertyPath == nameof(Node.Trigger)) {
					Document.Current?.ForceAnimationUpdate();
				}
			}

			protected override void InternalUndo(RemoveKeyframe op)
			{
				if (op.Animator.Owner == null) {
					op.AnimationHost.Animators.Add(op.Animator);
					Document.Current.RefreshSceneTree();
				}
				op.Animator.Keys.AddOrdered(op.Restore<Backup>().Keyframe);
				op.Animator.ResetCache();
				if (op.Animator.TargetPropertyPath == nameof(Node.Trigger)) {
					Document.Current?.ForceAnimationUpdate();
				}
			}
		}
	}

	public class AttemptToSetKeyFrameOutOfAnimationScopeException : Lime.Exception
	{
	}

	public sealed class SetKeyframe : Operation
	{
		public readonly IAnimationHost AnimationHost;
		public readonly string PropertyPath;
		public readonly string AnimationId;
		public readonly IKeyframe Keyframe;

		public override bool IsChangingDocument => true;

		public static void Perform(IAnimationHost animationHost, string propertyPath, Animation animation, IKeyframe keyframe)
		{
			if (!animation.IsLegacy && animation.Id != Animation.ZeroPoseId) {
				var animations = animation.Owner.Animations;
				// If there is a zero pose animation without corresponding keyframe -- create one.
				if (
					animations.TryFind(Animation.ZeroPoseId, out _) &&
					!animationHost.Animators.TryFind(propertyPath, out _, Animation.ZeroPoseId)
				) {
					var (propertyData, animable, index) = AnimationUtils.GetPropertyByPath(animationHost, propertyPath);
					var zeroPoseKey = Lime.Keyframe.CreateForType(propertyData.Info.PropertyType);
					zeroPoseKey.Value = index == -1 ? propertyData.Info.GetValue(animable) : propertyData.Info.GetValue(animable, new object [] { index });
					zeroPoseKey.Function = KeyFunction.Step;
					DocumentHistory.Current.Perform(new SetKeyframe(animationHost, propertyPath, Animation.ZeroPoseId, keyframe));
				}
			}
			if (!animation.IsLegacy && !CheckAnimationScope(animation, animationHost)) {
				throw new AttemptToSetKeyFrameOutOfAnimationScopeException();
			}
			DocumentHistory.Current.Perform(new SetKeyframe(animationHost, propertyPath, animation.Id, keyframe));
		}

		public static bool CheckAnimationScope(Animation animation, IAnimationHost animationHost)
		{
			if (animationHost is Node node) {
				for (var n = node.Parent; n != null; n = n.Parent) {
					if (n.Animations.TryFind(animation.Id, out var a)) {
						return a == animation;
					}
				}
				return false;
			}
			return true;
		}

		public static void Perform(IAnimator animator, Animation animation, IKeyframe keyframe)
		{
			if (animator.AnimationId != animation.Id) {
				throw new InvalidOperationException();
			}
			Perform(animator.Owner, animator.TargetPropertyPath, animation, keyframe);
		}

		private SetKeyframe(IAnimationHost animationHost, string propertyPath, string animationId, IKeyframe keyframe)
		{
			AnimationHost = animationHost;
			PropertyPath = propertyPath;
			Keyframe = keyframe;
			AnimationId = animationId;
		}

		public sealed class Processor : OperationProcessor<SetKeyframe>
		{
			class Backup
			{
				public IKeyframe Keyframe;
				public bool AnimatorExists;
				public IAnimator Animator;
				public object ValueWhenNoAnimator;
			}

			protected override void InternalRedo(SetKeyframe op)
			{
				Backup backup;
				IAnimator animator;

				if (!op.Find(out backup)) {
					bool animatorExists =
						op.AnimationHost.Animators.Any(a => a.TargetPropertyPath == op.PropertyPath && a.AnimationId == op.AnimationId);
					animator = op.AnimationHost.Animators[op.PropertyPath, op.AnimationId];
					var (propertyData, animable, index) = AnimationUtils.GetPropertyByPath(op.AnimationHost, op.PropertyPath);
					var value = index == -1 ? propertyData.Info.GetValue(animable) : propertyData.Info.GetValue(animable, new object [] { index });
					op.Save(new Backup {
						AnimatorExists = animatorExists,
						Animator = animator,
						Keyframe = animator.Keys.GetByFrame(op.Keyframe.Frame),
						ValueWhenNoAnimator = !animatorExists ? value : null,
					});
					if (!animatorExists) {
						Document.Current.RefreshSceneTree();
					}
				} else {
					animator = backup.Animator;
					if (!backup.AnimatorExists) {
						op.AnimationHost.Animators.Add(animator);
						Document.Current.RefreshSceneTree();
					}
				}

				animator.Keys.AddOrdered(op.Keyframe);
				animator.ResetCache();
				if (animator.TargetPropertyPath == nameof(Node.Trigger)) {
					Document.Current?.ForceAnimationUpdate();
				}
			}

			protected override void InternalUndo(SetKeyframe op)
			{
				var b = op.Peek<Backup>();
				var key = b.Animator.Keys.GetByFrame(op.Keyframe.Frame);
				if (key == null) {
					throw new InvalidOperationException();
				}
				b.Animator.Keys.Remove(key);
				if (b.Keyframe != null) {
					b.Animator.Keys.AddOrdered(b.Keyframe);
				}
				if (!b.AnimatorExists || b.Animator.Keys.Count == 0) {
					op.AnimationHost.Animators.Remove(b.Animator);
					Document.Current.RefreshSceneTree();
					var (propertyData, animable, index) = AnimationUtils.GetPropertyByPath(op.AnimationHost, op.PropertyPath);
					if (index == -1) {
						propertyData.Info.SetValue(animable, b.ValueWhenNoAnimator);
					} else {
						propertyData.Info.SetValue(animable, b.ValueWhenNoAnimator, new object[] {index});
					}
				}
				b.Animator.ResetCache();
				if (b.Animator.TargetPropertyPath == nameof(Node.Trigger)) {
					Document.Current?.ForceAnimationUpdate();
				}
			}
		}
	}

	public sealed class InsertIntoList : Operation
	{
		public readonly IList List;
		public readonly int Index;
		public readonly object Element;

		public override bool IsChangingDocument => true;

		private InsertIntoList(IList list, int index, object element)
		{
			List = list;
			Index = index;
			Element = element;
		}

		public static void Perform(IList list, int index, object element) => DocumentHistory.Current.Perform(new InsertIntoList(list, index, element));

		public sealed class Processor : OperationProcessor<InsertIntoList>
		{
			protected override void InternalRedo(InsertIntoList op) => op.List.Insert(op.Index, op.Element);
			protected override void InternalUndo(InsertIntoList op) => op.List.RemoveAt(op.Index);
		}
	}

	public sealed class RemoveFromList : Operation
	{
		public readonly IList List;
		public readonly int Index;
		private object backup;

		public override bool IsChangingDocument => true;

		private RemoveFromList(IList list, int index)
		{
			List = list;
			Index = index;
		}

		public static void Perform(IList list, int index) => DocumentHistory.Current.Perform(new RemoveFromList(list, index));

		public sealed class Processor : OperationProcessor<RemoveFromList>
		{
			protected override void InternalRedo(RemoveFromList op)
			{
				op.backup = op.List[op.Index];
				op.List.RemoveAt(op.Index);
			}

			protected override void InternalUndo(RemoveFromList op) => op.List.Insert(op.Index, op.backup);
		}
	}

	public sealed class InsertIntoList<TList, TElement> : Operation where TList : IList<TElement>
	{
		public readonly TList List;
		public readonly int Index;
		public readonly TElement Element;

		public override bool IsChangingDocument => true;

		private InsertIntoList(TList list, int index, TElement element)
		{
			List = list;
			Index = index;
			Element = element;
		}

		public static void Perform(TList list, int index, TElement element) => DocumentHistory.Current.Perform(new InsertIntoList<TList, TElement>(list, index, element));

		public sealed class Processor : OperationProcessor<InsertIntoList<TList, TElement>>
		{
			protected override void InternalRedo(InsertIntoList<TList, TElement> op) => op.List.Insert(op.Index, op.Element);
			protected override void InternalUndo(InsertIntoList<TList, TElement> op) => op.List.RemoveAt(op.Index);
		}
	}

	public sealed class RemoveFromList<TList, TElement> : Operation where TList : IList<TElement>
	{
		public readonly TList List;
		public readonly int Index;
		private TElement backup;

		public override bool IsChangingDocument => true;

		private RemoveFromList(TList list, int index)
		{
			List = list;
			Index = index;
		}

		public static void Perform(TList list, TElement item) => Perform(list, list.IndexOf(item));

		public static void Perform(TList list, int index) => DocumentHistory.Current.Perform(new RemoveFromList<TList, TElement>(list, index));

		public sealed class Processor : OperationProcessor<RemoveFromList<TList, TElement>>
		{
			protected override void InternalRedo(RemoveFromList<TList, TElement> op)
			{
				op.backup = op.List[op.Index];
				op.List.RemoveAt(op.Index);
			}

			protected override void InternalUndo(RemoveFromList<TList, TElement> op) => op.List.Insert(op.Index, op.backup);
		}
	}

	public sealed class AddIntoCollection<TCollection, TElement> : Operation where TCollection : ICollection<TElement>
	{
		public readonly TCollection Collection;
		public readonly TElement Element;

		public override bool IsChangingDocument => true;

		private AddIntoCollection(TCollection collection, TElement element)
		{
			Collection = collection;
			Element = element;
		}

		public static void Perform(TCollection collection, TElement element) =>
			DocumentHistory.Current.Perform(new AddIntoCollection<TCollection, TElement>(collection, element));

		public sealed class Processor : OperationProcessor<AddIntoCollection<TCollection, TElement>>
		{
			protected override void InternalRedo(AddIntoCollection<TCollection, TElement> op) => op.Collection.Add(op.Element);
			protected override void InternalUndo(AddIntoCollection<TCollection, TElement> op) => op.Collection.Remove(op.Element);
		}
	}

	public sealed class RemoveFromCollection<TCollection, TElement> : Operation where TCollection : ICollection<TElement>
	{
		public readonly TCollection Collection;
		public readonly TElement Element;

		public override bool IsChangingDocument => true;

		private RemoveFromCollection(TCollection collection, TElement element)
		{
			Collection = collection;
			Element = element;
		}

		public static void Perform(TCollection collection, TElement element) =>
			DocumentHistory.Current.Perform(new RemoveFromCollection<TCollection, TElement>(collection, element));

		public sealed class Processor : OperationProcessor<RemoveFromCollection<TCollection, TElement>>
		{
			protected override void InternalRedo(RemoveFromCollection<TCollection, TElement> op) => op.Collection.Remove(op.Element);
			protected override void InternalUndo(RemoveFromCollection<TCollection, TElement> op) => op.Collection.Add(op.Element);
		}
	}

	public sealed class InsertIntoDictionary<TDictionary, TKey, TValue> : Operation where TDictionary : IDictionary<TKey, TValue>, IDictionary
	{
		public readonly TDictionary Dictionary;
		public readonly TKey Key;
		public readonly TValue Value;
		public readonly TValue OldValue;
		public readonly bool HadValue;

		public override bool IsChangingDocument => true;

		private InsertIntoDictionary(TDictionary dictionary, TKey key, TValue value)
		{
			Dictionary = dictionary;
			Key = key;
			Value = value;
			HadValue = dictionary.TryGetValue(key, out OldValue);
		}

		public static void Perform(TDictionary dictionary, TKey key, TValue value) =>
			DocumentHistory.Current.Perform(new InsertIntoDictionary<TDictionary, TKey, TValue>(dictionary, key, value));

		public sealed class Processor : OperationProcessor<InsertIntoDictionary<TDictionary, TKey, TValue>>
		{
			protected override void InternalRedo(InsertIntoDictionary<TDictionary, TKey, TValue> op) =>
				op.Dictionary[op.Key] = op.Value;

			protected override void InternalUndo(InsertIntoDictionary<TDictionary, TKey, TValue> op)
			{
				if (op.HadValue) {
					op.Dictionary[op.Key] = op.OldValue;
				} else {
					op.Dictionary.Remove(op.Key);
				}
			}
		}
	}

	public sealed class RemoveFromDictionary<TDictionary, TKey, TValue> : Operation where TDictionary : IDictionary<TKey, TValue>, IDictionary
	{
		public readonly TDictionary Dictionary;
		public readonly TKey Key;
		public readonly TValue Value;

		public override bool IsChangingDocument => true;

		private RemoveFromDictionary(TDictionary dictionary, TKey key)
		{
			Dictionary = dictionary;
			Key = key;
			Value = dictionary[key];
		}

		public static void Perform(TDictionary dictionary, TKey key) =>
			DocumentHistory.Current.Perform(new RemoveFromDictionary<TDictionary, TKey, TValue>(dictionary, key));

		public sealed class Processor : OperationProcessor<RemoveFromDictionary<TDictionary, TKey, TValue>>
		{
			protected override void InternalRedo(RemoveFromDictionary<TDictionary, TKey, TValue> op) =>
				op.Dictionary.Remove(op.Key);

			protected override void InternalUndo(RemoveFromDictionary<TDictionary, TKey, TValue> op) =>
				op.Dictionary.Add(op.Key, op.Value);
		}
	}

	public static class CreateNodeFromAsset
	{
		public static Node Perform(string assetPath)
		{
			var scene = Node.Load(assetPath);
			if (!NodeCompositionValidator.Validate(Document.Current.Container.GetType(), scene.GetType())) {
				throw new System.Exception($"Can't put {scene.GetType()} into {Document.Current.Container.GetType()}");
			}

			Node node;
			using (Document.Current.History.BeginTransaction()) {
				node = CreateNode.Perform(scene.GetType());
				SetProperty.Perform(node, nameof(Widget.ContentsPath), assetPath);
				SetProperty.Perform(node, nameof(Widget.Id), Path.GetFileNameWithoutExtension(assetPath));
				if (node is IPropertyLocker propertyLocker) {
					var id = propertyLocker.IsPropertyLocked("Id", true) ? scene.Id : Path.GetFileName(assetPath);
					SetProperty.Perform(node, nameof(Node.Id), id);
				}
				if (scene is Widget widget) {
					SetProperty.Perform(node, nameof(Widget.Pivot), Vector2.Half);
					SetProperty.Perform(node, nameof(Widget.Size), widget.Size);
				}
				node.LoadExternalScenes();
				SelectNode.Perform(node);
				Document.Current.History.CommitTransaction();
			}
			return node;
		}
	}

	public static class CreateTexturedWidgetFromAsset
	{
		public static Node Perform(string assetPath, Type imageType)
		{
			Node node;
			using (Document.Current.History.BeginTransaction()) {
				node = CreateNode.Perform(imageType);
				var texture = new SerializableTexture(assetPath);
				var nodeSize = (Vector2)texture.ImageSize;
				var nodeId = Path.GetFileNameWithoutExtension(assetPath);
				if (node is Widget) {
					SetProperty.Perform(node, nameof(Widget.Texture), texture);
					SetProperty.Perform(node, nameof(Widget.Pivot), Vector2.Half);
					SetProperty.Perform(node, nameof(Widget.Size), nodeSize);
					SetProperty.Perform(node, nameof(Widget.Id), nodeId);
				} else if (node is ParticleModifier) {
					SetProperty.Perform(node, nameof(ParticleModifier.Texture), texture);
					SetProperty.Perform(node, nameof(ParticleModifier.Size), nodeSize);
					SetProperty.Perform(node, nameof(ParticleModifier.Id), nodeId);
				}
				SelectNode.Perform(node);
				Document.Current.History.CommitTransaction();
			}
			return node;
		}
	}

	public static class CreateAnimationSequenceImageFromAssets
	{
		public static Node Perform(IReadOnlyCollection<string> assetPaths)
		{
			Node node;
			using (Document.Current.History.BeginTransaction()) {
				node = CreateNode.Perform(typeof(Image));
				SetProperty.Perform(node, nameof(Widget.Pivot), Vector2.Half);
				SetProperty.Perform(node, nameof(Widget.Id), "Temp");
				if (assetPaths.Count > 0) {
					var i = 0;
					ITexture first = null;
					foreach (var assetPath in assetPaths) {
						var texture = new SerializableTexture(assetPath);
						first = first ?? texture;
						SetKeyframe.Perform(
							node,
							nameof(Widget.Texture),
							Document.Current.Animation,
							new Keyframe<ITexture> {
								Value = texture,
								Frame = i++,
								Function = KeyFunction.Step,
							}
						);
					}
					SetProperty.Perform(node, nameof(Widget.Texture), first);
					SetProperty.Perform(node, nameof(Widget.Size), (Vector2)first.ImageSize);
				}
				Document.Current.History.CommitTransaction();
			}
			return node;
		}
	}

	public static class CreateAudioFromAsset
	{
		public static Node Perform(string assetPath)
		{
			Node node;
			using (Document.Current.History.BeginTransaction()) {
				node = CreateNode.Perform(typeof(Audio));
				var sample = new SerializableSample(assetPath);
				SetProperty.Perform(node, nameof(Audio.Sample), sample);
				SetProperty.Perform(node, nameof(Node.Id), Path.GetFileNameWithoutExtension(assetPath));
				SetProperty.Perform(node, nameof(Audio.Volume), 1);
				var key = new Keyframe<AudioAction> {
					Frame = Document.Current.AnimationFrame,
					Value = AudioAction.Play
				};
				SetKeyframe.Perform(node, nameof(Audio.Action), Document.Current.Animation, key);
				SelectNode.Perform(node);
				Document.Current.History.CommitTransaction();
			}
			return node;
		}
	}

	public static class CreateModel3DFromAsset
	{
		public static Node Perform(string assetPath)
		{
			Node node;
			using (Document.Current.History.BeginTransaction()) {
				node = CreateNode.Perform(typeof(Model3D));
				SetProperty.Perform(node, nameof(Node.Id), nameof(Model3D));
				SetProperty.Perform(node, nameof(Node.ContentsPath), assetPath);
				SelectNode.Perform(node);
				Document.Current.History.CommitTransaction();
			}
			return node;
		}
	}

	public static class CreateNode
	{
		public static Node Perform(Type nodeType, bool aboveSelected = true)
		{
			SceneTreeUtils.TryGetSceneItemLinkLocation(out var parent, out var index, nodeType, aboveSelected);
			return Perform(parent, index, nodeType);
		}

		public static Node Perform(SceneItem parent, SceneTreeIndex index, Type nodeType)
		{
			if (!nodeType.IsSubclassOf(typeof(Node))) {
				throw new InvalidOperationException();
			}
			if (Document.Current.Animation.IsCompound) {
				throw new InvalidOperationException("Can't create a node while animation editor is active.");
			}
			var hostNode = SceneTreeUtils.GetOwnerNodeSceneItem(parent).GetNode();
			var ctr = nodeType.GetConstructor(Type.EmptyTypes);
			var node = (Node)ctr.Invoke(new object[] { });
			if (!LinkSceneItem.CanLink(parent, node)) {
				throw new InvalidOperationException($"Can't add {nodeType} to '{parent.Id}'");
			}
			var attrs = ClassAttributes<TangerineNodeBuilderAttribute>.Get(nodeType);
			if (attrs?.MethodName != null) {
				var builder = nodeType.GetMethod(attrs.MethodName, BindingFlags.NonPublic | BindingFlags.Instance);
				builder.Invoke(node, new object[] { });
			}
			node.Id = GenerateNodeId(hostNode, nodeType);
			Document.Decorate(node);
			LinkSceneItem.Perform(parent, index, node);
			ClearSceneItemSelection.Perform();
			SelectNode.Perform(node);
			return node;
		}

		static string GenerateNodeId(Node container, Type nodeType)
		{
			int c = 1;
			var id = nodeType.Name;
			while (container.Nodes.Any(i => i.Id == id)) {
				id = nodeType.Name + c;
				c++;
			}
			return id;
		}
	}

	public sealed class SetMarker : Operation
	{
		private readonly Marker marker;
		private readonly bool removeDependencies;

		public override bool IsChangingDocument => true;

		private SetMarker(Marker marker, bool removeDependencies)
		{
			this.marker = marker;
			this.removeDependencies = removeDependencies;
		}

		public static void Perform(Marker marker, bool removeDependencies)
		{
			var previousMarker = Document.Current.Animation.Markers.GetByFrame(marker.Frame);

			DocumentHistory.Current.Perform(new SetMarker(marker, removeDependencies));

			if (removeDependencies) {
				// Detect if a previous marker id is unique then rename it in triggers and markers.
				if (previousMarker != null && previousMarker.Id != marker.Id &&
					Document.Current.Animation.Markers.All(markerEl => markerEl.Id != previousMarker.Id)) {

					foreach (var markerEl in Document.Current.Animation.Markers.ToList()) {
						if (markerEl.Action == MarkerAction.Jump && markerEl.JumpTo == previousMarker.Id) {
							SetProperty.Perform(markerEl, nameof(markerEl.JumpTo), marker.Id);
						}
					}

					ProcessAnimableProperty.Perform(Document.Current.Container, nameof(Node.Trigger),
						(string value, out string newValue) => {
							return TriggersValidation.TryRenameMarkerInTrigger(
								previousMarker.Id, marker.Id, value, out newValue
							);
						}
					);
				}
			}
		}

		public sealed class Processor : OperationProcessor<SetMarker>
		{
			private class Backup
			{
				internal Marker Marker;
				internal string SavedJumpTo;
			}

			protected override void InternalRedo(SetMarker op)
			{
				var backup = new Backup {
					Marker = Document.Current.Animation.Markers.GetByFrame(op.marker.Frame)
				};
				if (backup.Marker != null) {
					Document.Current.Animation.Markers.Remove(backup.Marker);
				}
				op.Save(backup);
				Document.Current.Animation.Markers.AddOrdered(op.marker);

				if (op.removeDependencies) {
					backup.SavedJumpTo = op.marker.JumpTo;
					if (op.marker.Action == MarkerAction.Jump &&
						Document.Current.Animation.Markers.All(markerEl => markerEl.Id != op.marker.JumpTo)) {
						op.marker.JumpTo = "";
					}
				}
				Document.Current.RefreshSceneTree();
			}

			protected override void InternalUndo(SetMarker op)
			{
				Document.Current.Animation.Markers.Remove(op.marker);
				var b = op.Restore<Backup>();
				if (b.Marker != null) {
					Document.Current.Animation.Markers.AddOrdered(b.Marker);
				}

				if (op.removeDependencies) {
					op.marker.JumpTo = b.SavedJumpTo;
				}
				Document.Current.RefreshSceneTree();
			}
		}

	}

	public sealed class DeleteMarker : Operation
	{
		private readonly Marker marker;
		private readonly bool removeDependencies;

		public override bool IsChangingDocument => true;

		public static void Perform(Marker marker, bool removeDependencies)
		{
			DocumentHistory.Current.Perform(new DeleteMarker(marker, removeDependencies));

			if (removeDependencies) {
				ProcessAnimableProperty.Perform(Document.Current.Container, nameof(Node.Trigger),
					(string value, out string newValue) => {
						return TriggersValidation.TryRemoveMarkerFromTrigger(marker.Id, value, out newValue);
					}
				);
			}
		}

		private DeleteMarker(Marker marker, bool removeDependencies)
		{
			this.marker = marker;
			this.removeDependencies = removeDependencies;
		}

		public sealed class Processor : OperationProcessor<DeleteMarker>
		{
			private class Backup
			{
				internal readonly List<Marker> RemovedJumpToMarkers;

				public Backup(List<Marker> removedJumpToMarkers)
				{
					RemovedJumpToMarkers = removedJumpToMarkers;
				}
			}

			protected override void InternalRedo(DeleteMarker op)
			{
				Document.Current.Animation.Markers.Remove(op.marker);

				if (op.removeDependencies) {
					var removedJumpToMarkers = new List<Marker>();
					for (int i = Document.Current.Animation.Markers.Count - 1; i >= 0; i--) {
						var marker = Document.Current.Animation.Markers[i];
						if (marker.Action != MarkerAction.Jump || marker.JumpTo != op.marker.Id) {
							continue;
						}
						removedJumpToMarkers.Insert(0, marker);
						marker.JumpTo = null;
					}
					op.Save(new Backup(removedJumpToMarkers));
				}
				Document.Current.RefreshSceneTree();
			}

			protected override void InternalUndo(DeleteMarker op)
			{
				Document.Current.Animation.Markers.AddOrdered(op.marker);

				Backup backup;
				if (op.Find(out backup)) {
					backup = op.Restore<Backup>();
					foreach (var marker in backup.RemovedJumpToMarkers) {
						marker.JumpTo = op.marker.Id;
					}
				}
				Document.Current.RefreshSceneTree();
			}

		}
	}

	public sealed class SetComponent : Operation
	{
		private readonly Node node;
		private readonly NodeComponent component;

		public override bool IsChangingDocument => true;

		private SetComponent(Node node, NodeComponent component)
		{
			this.node = node;
			this.component = component;
		}

		public static void Perform(Node node, NodeComponent component) => DocumentHistory.Current.Perform(new SetComponent(node, component));

		public sealed class Processor : OperationProcessor<SetComponent>
		{
			protected override void InternalRedo(SetComponent op) => op.node.Components.Add(op.component);
			protected override void InternalUndo(SetComponent op) => op.node.Components.Remove(op.component);
		}

	}

	public sealed class DeleteComponent : Operation
	{
		private readonly Node node;
		private readonly NodeComponent component;

		public override bool IsChangingDocument => true;

		private DeleteComponent(Node node, NodeComponent component)
		{
			this.node = node;
			this.component = component;
		}

		public static void Perform(Node node, NodeComponent component)
		{
			foreach (var item in Document.Current.VisibleSceneItems.ToList()) {
				if (item.TryGetAnimator(out var animator)) {
					var animable = animator.Animable;
					while (animable != null) {
						if (animable == component) {
							UnlinkSceneItem.Perform(item);
							break;
						}
						animable = animable.Owner;
					}
				}
			}
			DocumentHistory.Current.Perform(new DeleteComponent(node, component));
		}

		public sealed class Processor : OperationProcessor<DeleteComponent>
		{
			protected override void InternalRedo(DeleteComponent op) => op.node.Components.Remove(op.component);
			protected override void InternalUndo(DeleteComponent op) => op.node.Components.Add(op.component);
		}
	}

	public static class UntieWidgetsFromBones
	{
		public static void Perform(IEnumerable<Bone> bones, IEnumerable<Widget> widgets)
		{
			var sortedBones = bones.ToList();
			BoneUtils.SortBones(sortedBones);
			if (!widgets.Any() || !sortedBones.Any()) {
				return;
			}
			if (!CheckConsistency(bones, widgets)) throw new InvalidOperationException("Not all bones and widgets have the same parent");
			foreach (var widget in widgets.ToList()) {
				if (widget is DistortionMesh) {
					foreach (PointObject point in widget.Nodes) {
						UntieBonesFromNode(point, nameof(PointObject.SkinningWeights), sortedBones);
					}
				} else if (widget is Animesh animesh) {
					var boneIndices = bones.Select(bone => bone.Index).ToArray();
					for (var i = 0; i < animesh.Vertices.Count; i++) {
						var vertex = animesh.Vertices[i];
						var sw = vertex.SkinningWeights.Release(boneIndices);
						if (sw.IsEmpty()) {
							sw.Bone0.Weight = 1f;
						}
						vertex.SkinningWeights = sw;
						SetIndexedProperty.Perform(animesh.Vertices, "Item", i, vertex);
						if (animesh.TransientVertices != null) {
							var v = animesh.TransientVertices[i];
							v.SkinningWeights = sw;
							SetIndexedProperty.Perform(animesh.TransientVertices, "Item", i, v);
						}
						InvalidateAnimesh.Perform(animesh);
					}
					if (animesh.Animators.TryFind(nameof(Animesh.TransientVertices), out var animator)) {
						foreach (var key in animator.Keys.ToList()) {
							var value = (List<Animesh.SkinnedVertex>)key.Value;
							for (int i = 0; i < animesh.Vertices.Count; i++) {
								var vertex = animesh.Vertices[i];
								var v = value[i];
								v.SkinningWeights = vertex.SkinningWeights;
								SetIndexedProperty.Perform(value, "Item", i, v);
							}
						}
					}
				}  else {
					UntieBonesFromNode(widget, nameof(Widget.SkinningWeights), sortedBones);
				}
			}
		}

		private static bool CheckConsistency(IEnumerable<Bone> bones, IEnumerable<Widget> widgets)
		{
			var container = bones.First().Parent.AsWidget;
			foreach (var bone in bones) {
				if (bone.Parent == null || bone.Parent != container) return false;
			}

			foreach (var widget in widgets) {
				if (widget.Parent == null || widget.Parent != container) return false;
			}
			return true;
		}

		private static void UntieBonesFromNode(Node node, string skinningPropertyName, IEnumerable<Bone> bones)
		{
			var property = node.GetType().GetProperty(skinningPropertyName);
			var originSkinningWeights = (SkinningWeights)property.GetValue(node);
			var boneIndices = new List<int>();
			for (int i = 0; i < 4; i++) {
				if (bones.Any(b => b.Index == originSkinningWeights[i].Index)) {
					boneIndices.Add(i);
				}
			}
			if (boneIndices.Count != 0) {
				var skinningWeights = ResetSkinningWeights(boneIndices, originSkinningWeights);
				BakeSkinningTransform(skinningWeights, node);
				SetProperty.Perform(node, skinningPropertyName, skinningWeights);
			}
		}

		private static void BakeSkinningTransform(SkinningWeights newSkinningWeights, Node node)
		{
			if (node is PointObject) {
				var point = (PointObject) node;
				var originTranslation = point.TransformedPosition;
				var boneArray = node.Parent.Parent.AsWidget.BoneArray;
				var localToParentTransform = node.Parent.AsWidget.CalcLocalToParentTransform();
				var transformedPosition = originTranslation * localToParentTransform  *
					boneArray.CalcWeightedRelativeTransform(newSkinningWeights).CalcInversed() * localToParentTransform.CalcInversed();
				var translation = (transformedPosition - point.Offset) / point.Parent.AsWidget.Size;
				SetAnimableProperty.Perform(node, nameof(PointObject.Position), translation);
			} else {
				var widget = node.AsWidget;
				var originLocalToParent = node.AsWidget.CalcLocalToParentTransform();
				var transform = (originLocalToParent *
					widget.Parent.AsWidget.BoneArray.CalcWeightedRelativeTransform(newSkinningWeights).CalcInversed()).ToTransform2();
				SetAnimableProperty.Perform(node, nameof(Widget.Rotation), transform.Rotation);
				var localToParentTransform =
					Matrix32.Translation(-(widget.Pivot * widget.Size)) *
					Matrix32.Transformation(
						Vector2.Zero,
						widget.Scale,
						widget.Rotation * Mathf.Pi / 180f,
						Vector2.Zero);
				SetAnimableProperty.Perform(node, nameof(Widget.Position), transform.Translation - localToParentTransform.T);
			}
		}

		private static SkinningWeights ResetSkinningWeights(List<int> bonesIndices, SkinningWeights originSkinningWeights)
		{
			var skinningWeights = new SkinningWeights();
			var overallWeight = 0f;
			var newOverallWeight = 0f;
			for (var i = 0; i < 4; i++) {
				overallWeight += originSkinningWeights[i].Weight;
				if (bonesIndices.Contains(i)) {
					skinningWeights[i] = new BoneWeight();
				} else {
					skinningWeights[i] = originSkinningWeights[i];
					newOverallWeight += skinningWeights[i].Weight;
				}
			}
			if (Mathf.Abs(overallWeight) > Mathf.ZeroTolerance && Mathf.Abs(newOverallWeight) > Mathf.ZeroTolerance) {
				var factor = overallWeight / newOverallWeight;
				for (var i = 0; i < 4; i++) {
					var boneWeight = skinningWeights[i];
					boneWeight.Weight *= factor;
					skinningWeights[i] = boneWeight;
				}
			}
			return skinningWeights;
		}
	}

	public class TieWidgetsWithBonesException : Lime.Exception
	{
		public Node Node { get; set; }

		public TieWidgetsWithBonesException(Node node)
		{
			Node = node;
		}
	}

	public static class TieWidgetsWithBones
	{
		public static void Perform(IEnumerable<Bone> bones, IEnumerable<Widget> widgets)
		{
			var boneList = bones.ToList();
			if (!widgets.Any() || !bones.Any()) {
				return;
			}
			if (!BoneUtils.CheckConsistency(bones, widgets.ToArray())) {
				throw new InvalidOperationException("Not all bones and widgets have the same parent");
			}
			foreach (var widget in widgets) {
				if (widget is DistortionMesh mesh) {
					foreach (PointObject point in mesh.Nodes) {
						if (!CanApplyBone(point.SkinningWeights)) {
							throw new TieWidgetsWithBonesException(point);
						}
						SetProperty.Perform(point, nameof(PointObject.SkinningWeights),
							BoneUtils.CalcSkinningWeight(point.SkinningWeights, point.CalcPositionInSpaceOf(mesh.ParentWidget), boneList));
					}
				} else if (widget is Animesh animesh) {
					var localToParent = animesh.CalcLocalToParentTransform();
					for (var i = 0; i < animesh.Vertices.Count; i++) {
						var vertex = animesh.Vertices[i];
						var sw = vertex.SkinningWeights;
						if (!CanApplyBone(sw)) {
							throw new TieWidgetsWithBonesException(animesh);
						}
						vertex.SkinningWeights =
							BoneUtils.CalcSkinningWeight(vertex.SkinningWeights, localToParent.TransformVector(vertex.Pos), boneList);
						if (vertex.SkinningWeights.IsEmpty()) {
							vertex.BlendWeights.Weight0 = 1f;
						}
						SetIndexedProperty.Perform(animesh.Vertices, "Item", i, vertex);
						if (animesh.TransientVertices != null) {
							var v = animesh.TransientVertices[i];
							v.SkinningWeights = vertex.SkinningWeights;
							SetIndexedProperty.Perform(animesh.TransientVertices, "Item", i, v);
						}
						InvalidateAnimesh.Perform(animesh);
					}
					if (animesh.Animators.TryFind(nameof(Animesh.TransientVertices), out var animator)) {
						foreach (var key in animator.Keys.ToList()) {
							var value = (List<Animesh.SkinnedVertex>)key.Value;
							for (int i = 0; i < animesh.Vertices.Count; i++) {
								var vertex = animesh.Vertices[i];
								var v = value[i];
								v.SkinningWeights = vertex.SkinningWeights;
								SetIndexedProperty.Perform(value, "Item", i, v);
							}
						}
					}
				} else {
					if (!CanApplyBone(widget.SkinningWeights)) {
						throw new TieWidgetsWithBonesException(widget);
					}
					SetProperty.Perform(widget, nameof(Widget.SkinningWeights),
						BoneUtils.CalcSkinningWeight(widget.SkinningWeights, widget.Position, boneList));
				}
			}
			foreach (var bone in bones.ToList()) {
				var entry = bone.Parent.AsWidget.BoneArray[bone.Index];
				SetAnimableProperty.Perform(bone, nameof(Bone.RefPosition), entry.Joint, CoreUserPreferences.Instance.AutoKeyframes);
				SetAnimableProperty.Perform(bone, nameof(Bone.RefLength), entry.Length, CoreUserPreferences.Instance.AutoKeyframes);
				SetAnimableProperty.Perform(bone, nameof(Bone.RefRotation), entry.Rotation, CoreUserPreferences.Instance.AutoKeyframes);
			}
		}

		private static bool CanApplyBone(SkinningWeights skinningWeights)
		{
			for (var i = 0; i < 4; i++) {
				if (skinningWeights[i].Index == 0) {
					return true;
				}
			}
			return false;
		}
	}

	public static class TieSkinnedVerticesWithBones
	{
		public static void Perform(IEnumerable<Bone> bones, Lime.Animesh mesh, params int[] indices)
		{
			var sortedBones = bones.ToList();
			BoneUtils.SortBones(sortedBones);
			if (!sortedBones.Any()) {
				return;
			}
			if (!BoneUtils.CheckConsistency(bones, mesh)) {
				Console.WriteLine("Not all bones and meshes have the same parent");
				return;
			}
			var appliedBones = new HashSet<Bone>();
			var localToParent = mesh.CalcLocalToParentTransform();
			foreach (var index in indices) {
				var v = mesh.TransientVertices[index];
				var sw = v.SkinningWeights;
				var filteredBones = sortedBones.Where(i =>
					i.Index != sw.Bone0.Index &&
					i.Index != sw.Bone1.Index &&
					i.Index != sw.Bone2.Index &&
					i.Index != sw.Bone3.Index
				).ToList();
				if (filteredBones.Count > 0) {
					filteredBones.ForEach(i => appliedBones.Add(i));
					sw = BoneUtils.CalcSkinningWeight(sw, localToParent.TransformVector(v.Pos), filteredBones);
					if (sw.IsEmpty()) {
						sw.Bone0.Weight = 1f;
					}
					v.SkinningWeights = sw;
					mesh.Vertices[index] = v;
					v = mesh.TransientVertices[index];
					v.SkinningWeights = sw;
					mesh.TransientVertices[index] = v;
				}
			}

			foreach (var bone in appliedBones) {
				var entry = bone.Parent.AsWidget.BoneArray[bone.Index];
				SetAnimableProperty.Perform(bone, nameof(Bone.RefPosition), entry.Joint, CoreUserPreferences.Instance.AutoKeyframes);
				SetAnimableProperty.Perform(bone, nameof(Bone.RefLength), entry.Length, CoreUserPreferences.Instance.AutoKeyframes);
				SetAnimableProperty.Perform(bone, nameof(Bone.RefRotation), entry.Rotation, CoreUserPreferences.Instance.AutoKeyframes);
			}
		}
	}

	public static class UntieSkinnedVerticesFromBones
	{
		public static void Perform(IEnumerable<Bone> bones, Lime.Animesh mesh, params int[] indices)
		{
			var sortedBones = bones.ToList();
			BoneUtils.SortBones(sortedBones);
			if (!sortedBones.Any()) {
				return;
			}
			if (!BoneUtils.CheckConsistency(bones, mesh)) {
				Console.WriteLine("Not all bones and meshes have the same parent");
				return;
			}
			var boneIndices = bones.Select(i => i.Index).ToArray();
			foreach (var index in indices) {
				var v = mesh.Vertices[index];
				var sw = v.SkinningWeights.Release(boneIndices);
				if (sw.IsEmpty()) {
					sw.Bone0.Weight = 1f;
				}
				v.SkinningWeights = sw;
				mesh.Vertices[index] = v;
				v = mesh.TransientVertices[index];
				v.SkinningWeights = sw;
				mesh.TransientVertices[index] = v;
			}
		}
	}

	public sealed class InvalidateAnimesh : Operation
	{
		public override bool IsChangingDocument => false;
		private readonly Animesh animesh;

		private InvalidateAnimesh(Animesh animesh)
		{
			this.animesh = animesh;
		}

		public static void Perform(Animesh animesh) => DocumentHistory.Current.Perform(new InvalidateAnimesh(animesh));

		public sealed class Processor : OperationProcessor<InvalidateAnimesh>
		{
			protected override void InternalRedo(InvalidateAnimesh op) => op.animesh.Invalidate();
			protected override void InternalUndo(InvalidateAnimesh op) => op.animesh.Invalidate();
		}
	}

	public static class PropagateMarkers
	{
		public static void Perform(Node node)
		{
			EnterNode.Perform(node);
			foreach (var m in node.DefaultAnimation.Markers) {
				SetMarker.Perform(m.Clone(), true);
				SetKeyframe.Perform(node, nameof(Node.Trigger), null, new Keyframe<string> {
					Frame = m.Frame,
					Value = m.Id,
					Function = KeyFunction.Linear
				});
			}
			LeaveNode.Perform();
		}
	}

	public static class Flip
	{
		public static void Perform(IEnumerable<Node> nodes, Widget container, bool flipX, bool flipY)
		{
			if (!flipX && !flipY) return;
			foreach (var widget in nodes.OfType<Widget>()) {
				var s = widget.Scale;
				if (flipX) {
					s.X = -s.X;
				}
				if (flipY) {
					s.Y = -s.Y;
				}
				SetAnimableProperty.Perform(widget, nameof(Widget.Scale), s);
			}
			FlipBones.Perform(nodes, container, flipX, flipY);
		}
	}

	public static class FlipBones
	{
		public static void Perform(IEnumerable<Node> nodes, Widget container, bool flipX, bool flipY)
		{
			if (!flipX && !flipY) return;
			var roots = new List<Bone>();
			foreach (var bone in nodes.OfType<Bone>()) {
				var root = BoneUtils.FindBoneRoot(bone, container.Nodes);
				if (!roots.Contains(root)) {
					if (flipX && flipY) {
						SetAnimableProperty.Perform(root, nameof(Bone.Rotation), root.Rotation + 180);
					} else {
						SetAnimableProperty.Perform(root, nameof(Bone.Rotation), (flipY ? 180 : 0) - root.Rotation);
						SetAnimableProperty.Perform(root, nameof(Bone.Length), -root.Length);
						var bones = FindBoneDescendats(root, Document.Current.Container.Nodes.OfType<Bone>());
						foreach (var childBone in bones) {
							SetAnimableProperty.Perform(childBone, nameof(Bone.Rotation), -childBone.Rotation);
							SetAnimableProperty.Perform(childBone, nameof(Bone.Length), -childBone.Length);
						}
					}
					roots.Add(root);
				}
			}
		}

		private static IEnumerable<Bone> FindBoneDescendats(Bone root, IEnumerable<Bone> bones)
		{
			foreach (var bone in bones.Where(b => b.BaseIndex == root.Index)) {
				yield return bone;
				foreach (var b in FindBoneDescendats(bone, bones)) {
					yield return b;
				}
			}
		}
	}
}
