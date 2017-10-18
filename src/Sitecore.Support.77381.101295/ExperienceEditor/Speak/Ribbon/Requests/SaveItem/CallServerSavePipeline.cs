namespace Sitecore.Support.ExperienceEditor.Speak.Ribbon.Requests.SaveItem
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Caching;
  using Data;
  using Sitecore.ExperienceEditor.Speak.Server.Requests;
  using Sitecore.ExperienceEditor.Speak.Server.Responses;
  using Sitecore.ExperienceEditor.Switchers;
  using Globalization;
  using Pipelines;
  using Server.Contexts;

  public class CallServerSavePipeline : PipelineProcessorRequest<PageContext>
  {
    public override PipelineProcessorResponseValue ProcessRequest()
    {
      FixValues(RequestContext.Item.Database, RequestContext.FieldValues);

      var value2 = new PipelineProcessorResponseValue();
      var pipeline = PipelineFactory.GetPipeline("saveUI");
      pipeline.ID = ShortID.Encode(ID.NewID);
      var saveArgs = RequestContext.GetSaveArgs();
      using (new ClientDatabaseSwitcher(RequestContext.Item.Database))
      {
        pipeline.Start(saveArgs);
        CacheManager.GetItemCache(RequestContext.Item.Database).Clear();
        value2.AbortMessage = Translate.Text(saveArgs.Error);
        return value2;
      }
    }

    private void FixValues(Database database, Dictionary<string, string> dictionaryForm)
    {
      var array = dictionaryForm.Keys.ToArray();
      var array2 = array;
      foreach (var text in array2)
      {
        if (!text.StartsWith("fld_", StringComparison.InvariantCulture) &&
            !text.StartsWith("flds_", StringComparison.InvariantCulture)) continue;
        var text2 = text;
        var num = text2.IndexOf('$');
        if (num >= 0)
        {
          text2 = StringUtil.Left(text2, num);
        }
        var array3 = text2.Split('_');
        var iD = ShortID.DecodeID(array3[1]);
        var iD2 = ShortID.DecodeID(array3[2]);
        var item = database.GetItem(iD);
        if (item == null) continue;
        var field = item.Fields[iD2];
        var typeKey = field.TypeKey;
        if (typeKey != null && typeKey.Equals("single-line text", StringComparison.InvariantCultureIgnoreCase))
        {
          dictionaryForm[text] = StringUtil.RemoveTags(dictionaryForm[text]);
        }
      }
    }
  }


}