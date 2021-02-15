using System;
using MonoTouch.ObjCRuntime;

[assembly: LinkWith ("libLemonNative.a", LinkTarget.ArmV6 | LinkTarget.ArmV7, ForceLoad = true)]
