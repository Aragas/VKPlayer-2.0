using Rainmeter.AudioPlayer;

using VkNet;
using VkNet.Enums.Filters;
using VkNet.Exception;
using VKPlayer.Forms;

namespace VKPlayer.Plugin
{
    public static class MeasureHandler
    {
        internal static VkApi API = new VkApi();
        private static VKAuthorization AuthorizationForm;

        public static void Start(string command)
        {
            if (!API.IsAuthorized)
            {
                API.OnTokenExpires += API_OnTokenExpires;

                AuthorizationForm = new VKAuthorization();
                AuthorizationForm.ConfirmClicked += AuthorizationForm_SubmitClicked;
                AuthorizationForm.ShowDialog();

                if (!API.IsAuthorized)
                    return;

                Player.Execute(command);
            }
            else
            {
                Player.Execute(command);
            }
        }

        private static void API_OnTokenExpires(VkApi api)
        {
            AuthorizationForm = new VKAuthorization();
            AuthorizationForm.ConfirmClicked += AuthorizationForm_SubmitClicked;
            AuthorizationForm.ShowDialog();
        }

        private static void AuthorizationForm_SubmitClicked(string login, string pass, string twofactor)
        {
            AuthorizationForm.Close();

            try
            {
                API.Authorize(new ApiAuthParams
                {
                    ApplicationId = 3328403,
                    Login = login,
                    Password = pass,
                    Settings = Settings.Audio,
                    TwoFactorAuthorization = () => twofactor,
                });
            }
            catch (VkApiAuthorizationException) { }
        }
    }
}