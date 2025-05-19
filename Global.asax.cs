using System;
using System.Web;
using System.Web.Optimization;

namespace WebForms
{
	public class Global : HttpApplication {
		void Application_Start(object sender, EventArgs e) {
			BundleConfig.RegisterBundles(BundleTable.Bundles);
		}
	}
}
