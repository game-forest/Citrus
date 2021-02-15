using System;
using MonoTouch.ObjCRuntime;

[assembly: LinkWith ("libLemonNativeSim.a", LinkTarget.Simulator, ForceLoad = true)]
