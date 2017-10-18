namespace Sitecore.Support.ExperienceEditor.Speak.Server.Contexts
{
  using Newtonsoft.Json;
  using Collections;
  using Data;
  using Data.Validators;
  using Diagnostics;
  using Sitecore.ExperienceEditor.Speak.Server.Contexts;
  using Sitecore.ExperienceEditor.Utils;
  using Pipelines.Save;
  using System.Collections.Generic;

  public class PageContext : ItemContext
  {
    [JsonProperty("scLayout")]
    public string LayoutSource
    {
      get;
      set;
    }

    [JsonProperty("scValidatorsKey")]
    public string ValidatorsKey
    {
      get;
      set;
    }

    [JsonProperty("scFieldValues")]
    public Dictionary<string, string> FieldValues
    {
      get;
      set;
    }

    public SaveArgs GetSaveArgs()
    {
      var fields = Utils.WebUtility.GetFields(Item.Database, FieldValues);
      var empty = string.Empty;
      var layoutSource = LayoutSource;
      var saveArgs = PipelineUtil.GenerateSaveArgs(Item, fields, empty, layoutSource, string.Empty, Utils.WebUtility.GetCurrentLayoutFieldId().ToString());
      saveArgs.HasSheerUI = false;
      var parseXml = new ParseXml();
      parseXml.Process(saveArgs);
      return saveArgs;
    }

    public SafeDictionary<FieldDescriptor, string> GetControlsToValidate()
    {
      var item = Item;
      Assert.IsNotNull(item, "The item is null.");
      var fields = Utils.WebUtility.GetFields(item.Database, FieldValues);
      var safeDictionary = new SafeDictionary<FieldDescriptor, string>();
      foreach (var current in fields)
      {
        var item2 = (item.ID == current.ItemID) ? item : item.Database.GetItem(current.ItemID);
        var field = item.Fields[current.FieldID];
        var value = Utils.WebUtility.HandleFieldValue(current.Value, field.TypeKey);
        var key = new FieldDescriptor(item2.Uri, field.ID, value, false);
        var text = current.ControlId ?? string.Empty;
        safeDictionary[key] = text;
        if (!string.IsNullOrEmpty(text))
        {
          RuntimeValidationValues.Current[text] = value;
        }
      }
      return safeDictionary;
    }
  }
}