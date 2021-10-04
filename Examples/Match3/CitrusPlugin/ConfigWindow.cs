using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Orange;
using Lime;
using System.IO;

namespace CitrusPlugin
{
	public class ConfigWindow
	{
		public class Option
		{
			public string Arg;
			public string Description;
			public ICheckBox CheckBox;

			public Option(string arg, string description)
			{
				Arg = arg;
				Description = description;
			}
		}

		private readonly List<Option> options = new List<Option>();

		public ConfigWindow(IPluginUIBuilder uiBuilder)
		{
			var panel = uiBuilder.SidePanel;
			panel.Enabled = true;
			panel.Title = "Run Config";
			LoadOptions();
			foreach (var option in options)
			{
				var checkBox = panel.AddCheckBox(option.Description);
				option.CheckBox = checkBox;
				checkBox.Toggled += (o, args) => {
					SaveConfig();
				};
			}
			LoadConfig();
		}

		private void LoadOptions()
		{
			string path = The.Workspace.AssetsDirectory + "/CommandLine.xml";
			if (File.Exists(path)) {
				XmlDocument doc = new XmlDocument();
				doc.Load(path);
				XmlNodeList optionNodes = doc.DocumentElement.SelectNodes("/Config/Option");
				foreach (XmlNode node in optionNodes) {
					string arg = node.Attributes["Arg"].Value;
					string description = node.Attributes["Description"].Value;
					options.Add(new Option(arg, description));
				}
			}
		}

		public static string GetDataFilePath()
		{
			return Path.Combine(Lime.Environment.GetDataDirectory("Game Forest", "Match3", "1.0"), "config.xml");
		}

		private void LoadConfig()
		{
			var path = GetDataFilePath();
			if (File.Exists(path)) {
				XmlDocument doc = new XmlDocument();
				doc.Load(path);
				XmlNodeList optionNodes = doc.DocumentElement.SelectNodes("/Config/Option");
				foreach (XmlNode node in optionNodes) {
					string arg = node.Attributes["Arg"].Value;
					bool isSet = bool.Parse(node.Attributes["IsSet"].Value);
					Option option = options.Find((x => x.Arg == arg));
					if (option != null)
						option.CheckBox.Active = isSet;
				}
			}
		}

		private void SaveConfig()
		{
			using (XmlTextWriter writer = new XmlTextWriter(GetDataFilePath(), Encoding.UTF8)) {
				writer.WriteStartDocument();
				writer.WriteStartElement("Config");
				foreach (Option option in options) {
					writer.WriteStartElement("Option");
					writer.WriteAttributeString("Arg", option.Arg);
					writer.WriteStartAttribute("IsSet");
					writer.WriteValue(option.CheckBox.Active);
					writer.WriteEndAttribute();
					writer.WriteEndElement();
				};
				writer.WriteEndElement();
				writer.Close();
			};
		}

		public string GetCommandLineArguments()
		{
			string result = "";
			foreach (Option option in options) {
				if (option.CheckBox.Active)
					result = result + option.Arg + " ";
			}
			return result;
		}

	}
}
