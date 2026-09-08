using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Lesson.Startup))]
namespace Lesson
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            app.MapSignalR();
        }
    }
}
