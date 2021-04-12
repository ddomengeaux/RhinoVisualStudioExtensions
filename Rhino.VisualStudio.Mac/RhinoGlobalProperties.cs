﻿using System;
using System.Collections.Generic;
using Mono.Addins;
using MonoDevelop.Core;
using MonoDevelop.Projects.MSBuild;

namespace Rhino.VisualStudio.Mac
{
  [Extension("/MonoDevelop/ProjectModel/MSBuildGlobalPropertyProviders")]
  public class RhinoGlobalProperties : IMSBuildGlobalPropertyProvider
  {
    Dictionary<string, string> properties;

    static Dictionary<string, string> s_emptyDictionary = new Dictionary<string, string>();

#pragma warning disable 67
    public event EventHandler GlobalPropertiesChanged;
#pragma warning restore 67

    static RhinoGlobalProperties instance;

    static bool s_requiresMdb;
    public static bool RequiresMdb
    {
      get { return s_requiresMdb; }
      set
      {
        if (s_requiresMdb != value)
        {
          s_requiresMdb = value;
          Runtime.RunInMainThread(() => instance.GlobalPropertiesChanged?.Invoke(instance, EventArgs.Empty));
        }
      }
    }

    public RhinoGlobalProperties()
    {
      instance = this;
    }

    public IDictionary<string, string> GetGlobalProperties()
    {
      if (!RequiresMdb)
        return s_emptyDictionary;

      // in mono 5.0, it only generates mdb's if using mcs so switch to that compiler
      return properties ?? (properties = new Dictionary<string, string> {
          { "LangVersion" , "7.2" }, // mcs only supports up to C# v7.2 currently..
          { "CscToolExe" , "mcs.exe" },
          { "_DebugFileExt" , ".mdb" }
        });
    }
  }
}
