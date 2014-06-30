using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(DT2.Startup))]
namespace DT2
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}