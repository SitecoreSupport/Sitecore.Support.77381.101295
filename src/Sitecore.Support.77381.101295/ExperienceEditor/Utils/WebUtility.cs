namespace Sitecore.Support.ExperienceEditor.Utils
{
  using System;
  using System.Collections.Generic;
  using System.Collections.Specialized;
  using System.Globalization;
  using System.Linq;
  using System.Text.RegularExpressions;
  using System.Web;
  using System.Web.UI;
  using System.Xml;
  using Collections;
  using Data;
  using Data.Fields;
  using Data.Items;
  using Data.Validators;
  using Diagnostics;
  using Sitecore.ExperienceEditor.Exceptions;
  using Sitecore.ExperienceEditor.Extensions;
  using Sitecore.ExperienceEditor.Utils;
  using Globalization;
  using Links;
  using Pipelines;
  using Shell.Applications.WebEdit.Commands;
  using Shell.Framework.Commands;
  using Sites;
  using Text;
  using Web;
  using Web.Configuration;
  using Xml;

  public static class WebUtility
  {
    private static bool? isGetLayoutSourceFieldsExists;

    public static bool IsSublayoutInsertingMode => !string.IsNullOrEmpty(WebUtil.GetQueryString("sc_ruid"));

    public static string ClientLanguage => WebUtil.GetCookieValue("shell", "lang", Context.Language.Name);

    public static SiteInfo GetCurrentSiteInfo()
    {
      Assert.IsNotNull(Context.Request, "request");
      return SiteContextFactory.GetSiteInfo(string.IsNullOrEmpty(Context.Request.QueryString["sc_pagesite"]) ? Configuration.Settings.Preview.DefaultSite : Context.Request.QueryString["sc_pagesite"]);
    }

    public static bool IsLayoutPresetApplied()
    {
      return !IsSublayoutInsertingMode && !string.IsNullOrEmpty(Context.PageDesigner.PageDesignerHandle) && (!string.IsNullOrEmpty(WebUtil.GetSessionString(Context.PageDesigner.PageDesignerHandle)) && !string.IsNullOrEmpty(WebUtil.GetSessionString(Context.PageDesigner.PageDesignerHandle + "_SAFE")));
    }

    public static string GetDevice(UrlString url)
    {
      Assert.ArgumentNotNull(url, "url");
      var empty = string.Empty;
      var device = Context.Device;
      if (device != null)
      {
        url["dev"] = device.ID.ToString();
        empty = device.ID.ToShortID().ToString();
      }
      return Assert.ResultNotNull(empty);
    }

    public static void RenderLoadingIndicator(HtmlTextWriter output)
    {
      new Page().LoadControl("~/sitecore/shell/client/Sitecore/ExperienceEditor/PageEditbar/LoadingIndicator.ascx").RenderControl(output);
    }

    public static void RenderLayout(Item item, HtmlTextWriter output, string siteName, string deviceId)
    {
      var json = ConvertToJson(FixEmptyPlaceholders(GetLayout(item)));
      output.Write("<input id=\"scLayout\" type=\"hidden\" value='" + json + "' />");
      output.Write("<input id=\"scDeviceID\" type=\"hidden\" value=\"" + StringUtil.EscapeQuote(deviceId) + "\" />");
      output.Write("<input id=\"scItemID\" type=\"hidden\" value=\"" + StringUtil.EscapeQuote(item.ID.ToShortID().ToString()) + "\" />");
      output.Write("<input id=\"scLanguage\" type=\"hidden\" value=\"" + StringUtil.EscapeQuote(item.Language.Name) + "\" />");
      output.Write("<input id=\"scSite\" type=\"hidden\" value=\"" + StringUtil.EscapeQuote(siteName) + "\" />");
    }

    private static string FixEmptyPlaceholders(string layout)
    {
      var xmlDocument = new XmlDocument();
      xmlDocument.LoadXml(layout);
      var xmlNodeList = xmlDocument.SelectNodes("//r[@ph='']");
      if (xmlNodeList != null)
      {
        foreach (XmlNode xmlNode in xmlNodeList)
        {
          if (xmlNode.Attributes == null) continue;
          var str = Context.Database.GetItem(new ID(xmlNode.Attributes["id"].Value)).Fields[Sitecore.ExperienceEditor.Constants.FieldNames.Placeholder].Value;
          if (!string.IsNullOrEmpty(str))
            xmlNode.Attributes["ph"].Value = str;
        }
      }
      layout = xmlDocument.OuterXml;
      return layout;
    }

    public static string ConvertToJson(string layout)
    {
      Assert.ArgumentNotNull(layout, "layout");
      return Assert.ResultNotNull(WebEditUtil.ConvertXMLLayoutToJSON(layout));
    }

    public static string GetLayout(Item item)
    {
      Assert.ArgumentNotNull(item, "item");
      return GetLayout(new LayoutField(item));
    }

    public static string GetLayout(Field field)
    {
      Assert.ArgumentNotNull(field, "field");
      return GetLayout(new LayoutField(field));
    }

    public static string GetLayout(LayoutField layoutField)
    {
      Assert.ArgumentNotNull(layoutField, "field");
      var result = layoutField.Value;
      if (!Context.PageDesigner.IsDesigning)
        return Assert.ResultNotNull(result);
      var pageDesignerHandle = Context.PageDesigner.PageDesignerHandle;
      if (string.IsNullOrEmpty(pageDesignerHandle))
        return Assert.ResultNotNull(result);
      var sessionString = WebUtil.GetSessionString(pageDesignerHandle);
      if (!string.IsNullOrEmpty(sessionString))
        result = sessionString;
      return Assert.ResultNotNull(result);
    }

    public static Dictionary<string, string> ConvertFormKeysToDictionary(NameValueCollection form)
    {
      Assert.ArgumentNotNull(form, "dictionaryForm");
      return form.Keys.Cast<string>().Where(key => !string.IsNullOrEmpty(key)).ToDictionary(key => key, key => form[key]);
    }

    public static IEnumerable<PageEditorField> GetFields(Database database, Dictionary<string, string> dictionaryForm)
    {
      Assert.ArgumentNotNull(dictionaryForm, "dictionaryForm");
      var pageEditorFieldList = new List<PageEditorField>();
      foreach (var key in dictionaryForm.Keys)
      {
        if (key.StartsWith("fld_", StringComparison.InvariantCulture) || key.StartsWith("flds_", StringComparison.InvariantCulture))
        {
          var text = key;
          var str1 = dictionaryForm[key];
          var length = text.IndexOf('$');
          if (length >= 0)
            text = StringUtil.Left(text, length);
          var strArray = text.Split('_');
          var itemId = ShortID.DecodeID(strArray[1]);
          var index = ShortID.DecodeID(strArray[2]);
          var language = Language.Parse(strArray[3]);
          var version = Data.Version.Parse(strArray[4]);
          var str2 = strArray[5];
          var obj = database.GetItem(itemId, language, version);
          if (obj != null)
          {
            var field = obj.Fields[index];
            if (key.StartsWith("flds_", StringComparison.InvariantCulture))
            {
              str1 = (string)WebUtil.GetSessionValue(str1);
              if (string.IsNullOrEmpty(str1))
                str1 = field.Value;
            }
            switch (field.TypeKey)
            {
              case "html":
              case "rich text":
                str1 = str1.TrimEnd(' ');
                break;
              case "text":
                str1 = StringUtil.RemoveTags(str1);
                break;
              case "multi-line text":
              case "memo":
                // Begin of Sitecore.Support.101295
                var regex = new Regex("<br.*?/*?>", RegexOptions.IgnoreCase);
                str1 = regex.Replace(str1, "\r\n");
                // End of Sitecore.Support.101295
                str1 = StringUtil.RemoveTags(new Regex("<br.*/*>", RegexOptions.IgnoreCase).Replace(str1, "\r\n"));
                break;
            }
            var pageEditorField = new PageEditorField()
            {
              ControlId = text,
              FieldID = index,
              ItemID = itemId,
              Language = language,
              Revision = str2,
              Value = str1,
              Version = version
            };
            pageEditorFieldList.Add(pageEditorField);
          }
        }
      }
      return pageEditorFieldList;
    }

    public static IEnumerable<PageEditorField> GetFields(Item item)
    {
      var pageEditorFieldList = new List<PageEditorField>();
      foreach (Field field in item.Fields)
      {
        var pageEditorField = new PageEditorField()
        {
          ControlId = null,
          FieldID = field.ID,
          ItemID = field.Item.ID,
          Language = field.Language,
          Revision = item[FieldIDs.Revision],
          Value = field.Value,
          Version = item.Version
        };
        pageEditorFieldList.Add(pageEditorField);
      }
      return pageEditorFieldList;
    }

    public static Packet CreatePacket(Database database, IEnumerable<PageEditorField> fields, out SafeDictionary<FieldDescriptor, string> controlsToValidate)
    {
      Assert.ArgumentNotNull(fields, "fields");
      var packet = new Packet();
      controlsToValidate = new SafeDictionary<FieldDescriptor, string>();
      foreach (var field in fields)
      {
        var index1 = AddField(database, packet, field);
        if (index1 != null)
        {
          var index2 = field.ControlId ?? string.Empty;
          controlsToValidate[index1] = index2;
          if (!string.IsNullOrEmpty(index2))
            RuntimeValidationValues.Current[index2] = index1.Value;
        }
      }
      return packet;
    }

    public static FieldDescriptor AddField(Database database, Packet packet, PageEditorField pageEditorField)
    {
      Assert.ArgumentNotNull(packet, "packet");
      Assert.ArgumentNotNull(pageEditorField, "pageEditorField");
      var obj1 = database.GetItem(pageEditorField.ItemID, pageEditorField.Language, pageEditorField.Version);
      if (obj1 == null)
        return null;
      var field = obj1.Fields[pageEditorField.FieldID];
      var valueToValidate = HandleFieldValue(pageEditorField.Value, field.TypeKey);
      var validationErrorMessage = GetFieldValidationErrorMessage(field, valueToValidate);
      if (validationErrorMessage != string.Empty)
        throw new FieldValidationException(validationErrorMessage, field);
      if (valueToValidate == field.Value)
      {
        var regexValidationError = FieldUtil.GetFieldRegexValidationError(field, valueToValidate);
        if (string.IsNullOrEmpty(regexValidationError))
          return new FieldDescriptor(obj1.Uri, field.ID, valueToValidate, field.ContainsStandardValue);
        if (obj1.Paths.IsMasterPart || StandardValuesManager.IsStandardValuesHolder(obj1))
          return new FieldDescriptor(obj1.Uri, field.ID, valueToValidate, field.ContainsStandardValue);
        throw new FieldValidationException(regexValidationError, field);
      }
      var xmlNode = packet.XmlDocument.SelectSingleNode("/*/field[@itemid='" + pageEditorField.ItemID + "' and @language='" + pageEditorField.Language + "' and @version='" + pageEditorField.Version + "' and @fieldid='" + pageEditorField.FieldID + "']");
      if (xmlNode != null)
      {
        var obj2 = database.GetItem(pageEditorField.ItemID, pageEditorField.Language, pageEditorField.Version);
        if (obj2 == null)
          return null;
        if (valueToValidate != obj2[pageEditorField.FieldID])
          xmlNode.ChildNodes[0].InnerText = valueToValidate;
      }
      else
      {
        packet.StartElement("field");
        packet.SetAttribute("itemid", pageEditorField.ItemID.ToString());
        packet.SetAttribute("language", pageEditorField.Language.ToString());
        packet.SetAttribute("version", pageEditorField.Version.ToString());
        packet.SetAttribute("fieldid", pageEditorField.FieldID.ToString());
        packet.SetAttribute("itemrevision", pageEditorField.Revision);
        packet.AddElement("value", valueToValidate);
        packet.EndElement();
      }
      return new FieldDescriptor(obj1.Uri, field.ID, valueToValidate, false);
    }

    public static string HandleFieldValue(string value, string fieldTypeKey)
    {
      switch (fieldTypeKey)
      {
        case "html":
        case "rich text":
          value = value.TrimEnd(' ');
          value = WebEditUtil.RepairLinks(value);
          break;
        case "text":
        case "single-line text":
          value = HttpUtility.HtmlDecode(value);
          break;
        case "integer":
        case "number":
          value = StringUtil.RemoveTags(value);
          break;
        case "multi-line text":
        case "memo":
          // Begin of Sitecore.Support.101295
          var regex = new Regex("<br.*?/*?>", RegexOptions.IgnoreCase);
          value = regex.Replace(value, "\r\n");
          // End of Sitecore.Support.101295
          value = StringUtil.RemoveTags(value);
          break;
        case "word document":
          value = string.Join(Environment.NewLine, value.Split(new[]
          {
            "\r\n",
            "\n\r",
            "\n"
          }, StringSplitOptions.None));
          break;
      }
      return value;
    }

    public static string GetFieldValidationErrorMessage(Field field, string value)
    {
      Assert.ArgumentNotNull(field, "field");
      Assert.ArgumentNotNull(value, "value");
      if (!Configuration.Settings.WebEdit.ValidationEnabled)
        return string.Empty;
      var cultureInfo = LanguageUtil.GetCultureInfo();
      if (value.Length == 0)
        return string.Empty;
      switch (field.TypeKey)
      {
        case "integer":
          long result1;
          if (long.TryParse(value, NumberStyles.Integer, cultureInfo, out result1))
            return string.Empty;
          return Translate.Text("\"{0}\" is not a valid integer.", (object)value);
        case "number":
          double result2;
          if (double.TryParse(value, NumberStyles.Float, cultureInfo, out result2))
            return string.Empty;
          return Translate.Text("\"{0}\" is not a valid number.", (object)value);
        default:
          return string.Empty;
      }
    }

    public static void AddLayoutField(string layout, Packet packet, Item item, string fieldId = null)
    {
      Assert.ArgumentNotNull(packet, "packet");
      Assert.ArgumentNotNull(item, "item");
      if (fieldId == null)
        fieldId = FieldIDs.FinalLayoutField.ToString();
      if (string.IsNullOrEmpty(layout))
        return;
      layout = WebEditUtil.ConvertJSONLayoutToXML(layout);
      Assert.IsNotNull(layout, layout);
      if (!IsEditAllVersionsTicked())
        layout = XmlDeltas.GetDelta(layout, new LayoutField(item.Fields[FieldIDs.LayoutField]).Value);
      packet.StartElement("field");
      packet.SetAttribute("itemid", item.ID.ToString());
      packet.SetAttribute("language", item.Language.ToString());
      packet.SetAttribute("version", item.Version.ToString());
      packet.SetAttribute("fieldid", fieldId);
      packet.AddElement("value", layout);
      packet.EndElement();
    }

    public static UrlString BuildChangeLanguageUrl(UrlString url, ItemUri itemUri, string languageName)
    {
      UrlString urlString;
      if (itemUri == null)
        return null;
      var site = SiteContext.GetSite(WebEditUtil.SiteName);
      if (site == null)
        return null;
      var itemNotNull = Client.GetItemNotNull(itemUri);
      using (new SiteContextSwitcher(site))
      {
        using (new LanguageSwitcher(itemNotNull.Language))
        {
          urlString = BuildChangeLanguageNewUrl(languageName, url, itemNotNull);
          if (LinkManager.LanguageEmbedding == LanguageEmbedding.Never)
            urlString["sc_lang"] = languageName;
          else
            urlString.Remove("sc_lang");
        }
      }
      return urlString;
    }

    public static string GetContentEditorDialogFeatures()
    {
      var str = "location=0,menubar=0,status=0,toolbar=0,resizable=1,getBestDialogSize:true";
      var device = Context.Device;
      if (device == null)
        return str;
      var capabilities = device.Capabilities as SitecoreClientDeviceCapabilities;
      if (capabilities == null || !capabilities.RequiresScrollbarsOnWindowOpen)
        return str;
      str += ",scrollbars=1,dependent=1";
      return str;
    }

    public static bool IsQueryStateEnabled<T>(Item contextItem) where T : Command, new()
    {
      return Activator.CreateInstance<T>().QueryState(new CommandContext(new[]
      {
        contextItem
      })) == CommandState.Enabled;
    }

    public static bool IsEditAllVersionsTicked()
    {
      if (StringUtility.EvaluateCheckboxRegistryKeyValue(Web.UI.HtmlControls.Registry.GetString(Sitecore.ExperienceEditor.Constants.RegistryKeys.EditAllVersions)))
        return IsEditAllVersionsAllowed();
      return false;
    }

    public static bool IsEditAllVersionsAllowed()
    {
      if (!isGetLayoutSourceFieldsExists.HasValue)
      {
        isGetLayoutSourceFieldsExists = CorePipelineFactory.GetPipeline("getLayoutSourceFields", string.Empty) != null;
        if (!isGetLayoutSourceFieldsExists.Value)
          Log.Warn("Pipeline getLayoutSourceFields is turned off.", new object());
      }
      return Context.Site != null && isGetLayoutSourceFieldsExists.Value && (Sitecore.ExperienceEditor.Settings.WebEdit.ExperienceEditorEditAllVersions && Context.Site.DisplayMode != DisplayMode.Normal) && WebUtil.GetQueryString("sc_disable_edit") != "yes" && WebUtil.GetQueryString("sc_duration") != "temporary";
    }

    public static ID GetCurrentLayoutFieldId()
    {
      if (!IsEditAllVersionsTicked())
        return FieldIDs.FinalLayoutField;
      return FieldIDs.LayoutField;
    }

    private static UrlString BuildChangeLanguageNewUrl(string languageName, UrlString url, Item item)
    {
      Language result;
      Assert.IsTrue(Language.TryParse(languageName, out result), $"Cannot parse the language ({languageName}).");
      var defaultOptions = UrlOptions.DefaultOptions;
      defaultOptions.Language = result;
      var obj = item.Database.GetItem(item.ID, result);
      Assert.IsNotNull(obj, $"Item not found ({item.ID}, {result}).");
      var itemUrl = LinkManager.GetItemUrl(obj, defaultOptions);
      var urlString = EnsureChangeLanguageUrlDomain(url, new UrlString(itemUrl));
      foreach (string key in url.Parameters.Keys)
        urlString.Parameters[key] = url.Parameters[key];
      return urlString;
    }

    private static UrlString EnsureChangeLanguageUrlDomain(UrlString oldUrl, UrlString newUrl)
    {
      if (newUrl.IsRelative() || oldUrl.IsRelative())
        return newUrl;
      var absoluteUri1 = oldUrl.ToAbsoluteUri(true);
      var absoluteUri2 = newUrl.ToAbsoluteUri(true);
      if (absoluteUri1.DnsSafeHost.Equals(absoluteUri2.DnsSafeHost, StringComparison.OrdinalIgnoreCase))
        return newUrl;
      return new UrlString(
        $"{absoluteUri2.Scheme}://{absoluteUri1.DnsSafeHost}/{absoluteUri2.AbsolutePath.TrimStart('/')}");
    }
  }
}
