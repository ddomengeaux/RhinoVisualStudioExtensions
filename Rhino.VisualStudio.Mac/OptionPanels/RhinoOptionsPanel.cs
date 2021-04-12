﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MonoDevelop.Components;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.Projects;

namespace Rhino.VisualStudio.Mac.OptionPanels
{
  internal class RhinoOptionsPanel : ItemOptionsPanel
  {

    static RhinoOptionsPanel()
    {
      EtoInitializer.Initialize();
    }

    RhinoOptionsPanelWidget widget;

    public override bool IsVisible()
    {
      return ConfiguredProject.DetectMcNeelProjectType() != null || ConfiguredProject.GetPluginProjectType() != null;
    }

    public override Control CreatePanelWidget()
    {
      // ugh, doesn't seem like native macOS widgets allowed here..
      widget = new RhinoOptionsPanelWidget((DotNetProject)ConfiguredProject, ItemConfigurations);

      return widget;
    }

    public override void ApplyChanges()
    {
      widget.Store();
    }
  }

  class RhinoOptionsPanelWidget : Gtk.VBox
  {
    Gtk.ComboBox pluginTypeCombo;
    Gtk.ComboBox launcherCombo;
    Gtk.Entry customLauncherEntry;
    Gtk.Button browseButton;
    Gtk.Label autodetectedTypeLabel;
    Gtk.Label bundleInformationLabel;
    bool pluginTypeChanged;
    bool launcherChanged;
    DotNetProject project;
    bool supportsDevelopment;
    string[] currentLauncherEntries = defaultLauncherEntries;
    static string[] typeEntries = { "Autodetect", "Rhino Plugin", "Grasshopper Component", "Library" };
    static string[] defaultLauncherEntries = { "Autodetect", /*"Rhinoceros", "RhinoWIP",*/ "Custom" };
    static string[] debugLauncherEntries = { "XCode" };

    string GetSelectedPluginType()
    {
      switch (pluginTypeCombo.Active)
      {
        case 0:
          return null;
        case 1:
          return "rhp";
        case 2:
          return "gha";
        case 3:
        default:
          return "none";
      }
    }

    int GetPluginComboIndex()
    {
      var type = project.GetPluginProjectType();
      if (type != null)
      {
        switch (type.Value)
        {
          case McNeelProjectType.None:
            return 3;
          case McNeelProjectType.RhinoCommon:
            return 1;
          case McNeelProjectType.Grasshopper:
            return 2;
        }
      }
      return 0;
    }

    int GetLauncherComboIndex()
    {
      var type = project.ProjectProperties.GetValue(Helpers.RhinoLauncherProperty);
      if (!string.IsNullOrEmpty(type))
      {
        switch (type.ToLowerInvariant())
        {
          // case "app":
          //   return 1;
          // case "wip":
          //   return 2;
          case "xcode":
            return supportsDevelopment ? 2 : 0;
          default:
            // custom
            return 1;
        }
      }
      return 0; // auto
    }

    string GetTypeLabel(McNeelProjectType type)
    {
      switch (type)
      {
        case McNeelProjectType.None:
          return "Library";
        case McNeelProjectType.DebugStarter:
          return "Rhino Development";
        case McNeelProjectType.RhinoCommon:
          return "Rhino Plugin";
        case McNeelProjectType.Grasshopper:
          return "Grasshopper Component";
        default:
          return null;
      }
    }

    string GetSelectedLauncher()
    {
      switch (launcherCombo.Active)
      {
        default:
        case 0:
          return null;
        // case 1:
        //   return "app";
        // case 2:
        //   return "wip";
        case 1:
          return customLauncherEntry.Text;
        case 2:
          return "xcode";
      }
    }

    public void Store()
    {
      if (pluginTypeChanged)
      {
        var pluginType = GetSelectedPluginType();
        if (pluginType != null)
          project.ProjectProperties.SetValue(Helpers.RhinoPluginTypeProperty, pluginType);
        else
          project.ProjectProperties.RemoveProperty(Helpers.RhinoPluginTypeProperty);

        project.NeedsReload = true;
        project.NotifyModified(Helpers.RhinoPluginTypeProperty);
      }

      if (launcherChanged)
      {
        var launcherType = GetSelectedLauncher();
        if (launcherType != null)
          project.ProjectProperties.SetValue(Helpers.RhinoLauncherProperty, launcherType);
        else
          project.ProjectProperties.RemoveProperty(Helpers.RhinoLauncherProperty);
        project.NotifyModified(Helpers.RhinoLauncherProperty);
      }
    }

    public RhinoOptionsPanelWidget(DotNetProject project, IEnumerable<ItemConfiguration> configurations)
    {
      supportsDevelopment = project.AsFlavor<RhinoProjectServiceExtension>()?.RhinoPluginType == McNeelProjectType.DebugStarter;

      if (supportsDevelopment)
        currentLauncherEntries = currentLauncherEntries.Concat(debugLauncherEntries).ToArray();

      this.project = project;
      Build();

      pluginTypeCombo.Active = GetPluginComboIndex();
      pluginTypeCombo.Changed += (sender, e) =>
      {
        pluginTypeChanged = true;
        SetAutodetectLabel();
      };
      SetAutodetectLabel();


      launcherCombo.Changed += (sender, e) =>
      {
        launcherChanged = true;
        SetCustomLauncherText();
      };
      launcherCombo.Active = GetLauncherComboIndex();
      SetCustomLauncherText();
    }

    void SetCustomLauncherText()
    {
      customLauncherEntry.IsEditable = launcherCombo.Active == 1; // custom
      browseButton.Sensitive = customLauncherEntry.IsEditable;
      var outputFileName = project.GetOutputFileName(project.DefaultConfiguration.Selector);
      switch (launcherCombo.Active)
      {
        case 0:
          // auto detect
          var version = project.GetRhinoVersion() ?? Helpers.DefaultRhinoVersion;
          var appPath = project.DetectApplicationPath(outputFileName, version);
          customLauncherEntry.Text = appPath;
          break;
        case 1:
          customLauncherEntry.Text = Helpers.StandardInstallPath;
          break;
        case 2:
          customLauncherEntry.Text = Helpers.StandardInstallWipPath;
          break;
        case 3:
          var currentLauncherIndex = GetLauncherComboIndex();
          if (currentLauncherIndex == 3) // custom
            customLauncherEntry.Text = project.ProjectProperties.GetValue(Helpers.RhinoLauncherProperty);
          else
            customLauncherEntry.Text = string.Empty;
          break;
        case 4:
          customLauncherEntry.Text = Helpers.GetXcodeDerivedDataPath(outputFileName);
          break;
      }

    }

    void SetBundleVersionLabel()
    {
      if (!string.IsNullOrEmpty(customLauncherEntry.Text))
      {
        var version = Helpers.GetVersionOfAppBundle(customLauncherEntry.Text);
        var versionString = version == null ? "unknown" : version.ToString();
        bundleInformationLabel.Markup = $"Application Version: <b>{versionString}</b>";
      }
      else
        bundleInformationLabel.Text = string.Empty;
    }

    void SetAutodetectLabel()
    {
      var sbInfo = new StringBuilder();
      if (pluginTypeCombo.Active == 0) // autodetect
      {
        var detectedType = project.DetectMcNeelProjectType();
        if (detectedType != null)
        {
          sbInfo.Append($"Type: <b>{GetTypeLabel(detectedType.Value)}</b>");
        }
      }

      var detectedVersion = project.GetRhinoVersion();
      if (detectedVersion != null)
      {
        if (sbInfo.Length > 0)
          sbInfo.Append(", ");
        sbInfo.Append($"Version: <b>{detectedVersion.Value}</b>");
      }

      if (sbInfo.Length > 0)
        sbInfo.Insert(0, "Detected ");

      autodetectedTypeLabel.Markup = sbInfo.ToString();
    }

    void Build()
    {
      Spacing = 6;

      //var heading = new Gtk.Label("<b>Rhino Options</b>");
      //heading.UseMarkup = true;
      //heading.Xalign = 0;



      pluginTypeCombo = new Gtk.ComboBox(typeEntries);

      launcherCombo = new Gtk.ComboBox(currentLauncherEntries);

      customLauncherEntry = new Gtk.Entry();
      customLauncherEntry.Changed += (sender, e) => SetBundleVersionLabel();

      var optionsBox = new Gtk.HBox();

      autodetectedTypeLabel = new Gtk.Label();

      bundleInformationLabel = new Gtk.Label();

      // layout

      //PackStart(heading, false, false, 0);


      var table = new Gtk.Table(4, 2, false);
      table.RowSpacing = 6;
      table.ColumnSpacing = 6;

      browseButton = new Gtk.Button();
      browseButton.Label = "Browse...";
      browseButton.Clicked += (sender, e) => {
        var fd = new Eto.Forms.OpenFileDialog();
        fd.Directory = new Uri("file:///Applications");
        fd.Filters.Add(new Eto.Forms.FileFilter("Application bundles", ".app"));
        if (fd.ShowDialog(null) == Eto.Forms.DialogResult.Ok)
        {
          customLauncherEntry.Text = fd.FileName;
        }
      };

      AddRow(table, 0, "Plugin Type:", AutoSized(6, pluginTypeCombo, autodetectedTypeLabel));
      AddRow(table, 1, "Launcher:", AutoSized(6, launcherCombo));
      AddRow(table, 2, "", AutoExpanded(6, customLauncherEntry, browseButton)); // add browse button?
      AddRow(table, 3, "", AutoSized(0, bundleInformationLabel));

      // indent
      optionsBox.PackStart(new Gtk.Label { WidthRequest = 18 }, false, false, 0);
      optionsBox.PackStart(table, true, true, 0);

      PackStart(optionsBox, true, true, 0);

      ShowAll();
    }

    void AddRow(Gtk.Table table, uint row, string label, Gtk.Widget widget)
    {
      table.Attach(new Gtk.Label(label ?? string.Empty) { Xalign = 1 }, 0, 1, row, row + 1, Gtk.AttachOptions.Shrink, Gtk.AttachOptions.Shrink, 0, 0);
      table.Attach(widget, 1, 2, row, row + 1, Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill, Gtk.AttachOptions.Shrink, 0, 0);
    }

    Gtk.HBox AutoSized(int spacing, params Gtk.Widget[] widgets)
    {
      var box = new Gtk.HBox();
      box.Spacing = spacing;
      foreach (var widget in widgets)
      {
        box.PackStart(widget, false, true, 0);
      }
      return box;
    }

    Gtk.HBox AutoExpanded(int spacing, Gtk.Widget expandedWidget, params Gtk.Widget[] widgets)
    {
      var box = new Gtk.HBox();
      box.Spacing = spacing;
      box.PackStart(expandedWidget, true, true, 0);
      foreach (var widget in widgets)
      {
        box.PackStart(widget, false, true, 0);
      }
      return box;
    }
  }
}
