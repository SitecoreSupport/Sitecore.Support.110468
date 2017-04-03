using Newtonsoft.Json.Linq;
using Sitecore.ContentTesting.Analytics.Reporting;
using Sitecore.ContentTesting.ComponentTesting;
using Sitecore.ContentTesting.Configuration;
using Sitecore.ContentTesting.Data;
using Sitecore.ContentTesting.Model.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Layouts;
using Sitecore.Pipelines.GetChromeData;
using System;
using System.Collections.Generic;
using System.Linq;
using Sitecore.ContentTesting;
using Sitecore.Globalization;
using Sitecore.Web;

namespace Sitecore.Support.ContentTesting.Pipelines.GetChromeData
{
    public class GetRenderingTestVariations : GetChromeDataProcessor
    {
        protected readonly IContentTestingFactory factory;

        protected readonly SitecoreContentTestStore testStore;

        public GetRenderingTestVariations()
            : this(null, null)
        {
        }

        public GetRenderingTestVariations(IContentTestingFactory factory, SitecoreContentTestStore testStore)
        {
            this.factory = (factory ?? ContentTestingFactory.Instance);
            this.testStore = (testStore ?? ((this.factory.ContentTestStore as SitecoreContentTestStore) ?? new SitecoreContentTestStore()));
        }

        public override void Process(GetChromeDataArgs args)
        {
            if (!Settings.IsAutomaticContentTestingEnabled)
            {
                return;
            }
            Assert.ArgumentNotNull(args, "args");
            Assert.IsNotNull(args.ChromeData, "Chrome Data");
            if (!"rendering".Equals(args.ChromeType, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            RenderingReference renderingReference = args.CustomData["renderingReference"] as RenderingReference;
            if (renderingReference == null)
            {
                return;
            }
            if (string.IsNullOrEmpty(renderingReference.Settings.MultiVariateTest))
            {
                return;
            }
            JArray jArray = this.ProcessTestingRendering(renderingReference);
            if (jArray != null && jArray.Count > 0)
            {
                args.ChromeData.Custom.Add("testVariations", jArray);
            }
        }

        protected virtual JArray ProcessTestingRendering(RenderingReference rendering)
        {
            TestVariationSelector testVariationSelector = new TestVariationSelector(rendering, this.testStore, this.factory);
            string text = Context.Request.QueryString["sc_lang"];
            Language language = null;
            if (!string.IsNullOrEmpty(text))
            {
                Language.TryParse(text, out language);
            }
            if (language == null)
            {
                language = Context.Data.FilePathLanguage;
            }
            if (language == null)
            {
                Language.TryParse(WebUtil.GetCookieValue(Context.Site.Name, "lang", Context.Site.Language), out language);
            }
            if (language == null || language == Language.Invariant)
            {
                return null;
            }

            MultivariateTestValueItem value = testVariationSelector.GetTestValueItem(language);
            if (value == null)
            {
                return null;
            }
            TestDefinitionItem testDefinition = ((TestValueItem)value).TestDefinition;
            IEnumerable<JObject> enumerable = from x in value.Variable.Values
                                              select JObject.FromObject(new
                                              {
                                                  guid = x.ID.ToGuid(),
                                                  id = x.ID.ToShortID().ToString(),
                                                  name = x.Name,
                                                  isActive = (x.ID == value.ID)
                                              });
            if (testDefinition.IsRunning)
            {
                ITestConfiguration testConfiguration = this.testStore.LoadTest(testDefinition, Context.Device.ID);
                if (testConfiguration != null)
                {
                    TestValueEngagementQuery testValueEngagementQuery = new TestValueEngagementQuery(testConfiguration, null);
                    testValueEngagementQuery.Execute();
                    foreach (JObject current in enumerable)
                    {
                        double valueEngagementScore = testValueEngagementQuery.GetValueEngagementScore(current.Value<Guid>("guid"));
                        current.Add("value", valueEngagementScore);
                    }
                }
            }
            return new JArray(enumerable);
        }
    }
}
