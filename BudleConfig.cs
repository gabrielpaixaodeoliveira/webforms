using System.Web.Optimization;
using System.Web.UI;

namespace WebForms
{

    public class BundleConfig
    {

        public static void RegisterBundles(BundleCollection bundles)
        {
            // Adicionar os bundles necessários
            bundles.Add(new ScriptBundle("~/bundles/lacuna").Include(
                "~/Scripts/lacuna-web-pki-2.11.0.js",
                "~/Scripts/App/signature-form.js"
            ));
        }
    }
}