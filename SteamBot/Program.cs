using System;
using System.Text;
using SteamKit2;
using System.IO;
using System.Threading;

namespace SteamBot
{
    class Program
    {
        static string user, pass;

        static SteamClient steamClient;
        static CallbackManager manager;
        static SteamUser steamUser;
        static SteamFriends steamFriends;

        static bool isRunning = false;

        static string authCode, twoFactorAuth;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("SteamBot Application has been developed by alien299 | Patrik Nagy");
            Console.WriteLine("https://patriknagy.hu");
            Console.WriteLine("https://alien299.itch.io");
            Console.WriteLine("https://github.com/alien299");
            Console.WriteLine("Version: 1.0");
            Console.ResetColor();
            


            Console.Title = "Alien SteamBot | by: alien299 / Patrik Nagy";
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("CTRL+C quits the program\n");
            Console.ResetColor();

            Console.Write("Username: ");
            user = Console.ReadLine();

            Console.Write("Password: ");
            pass = Console.ReadLine();

            

        SteamLogIn();
        }

        static void SteamLogIn()
        {
            steamClient = new SteamClient();

            manager = new CallbackManager(steamClient);

            steamUser = steamClient.GetHandler<SteamUser>();

            steamFriends = steamClient.GetHandler<SteamFriends>();

            manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

            manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

            manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
            manager.Subscribe<SteamFriends.FriendMsgCallback>(OnChatMessage);

            manager.Subscribe<SteamFriends.FriendsListCallback>(OnFriendsList);
            manager.Subscribe<SteamFriends.FriendAddedCallback>(OnFriendAdded);

            manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(UpdateMachineAuthCallback);

            isRunning = true;

            Console.WriteLine("\nConnecting to Steam...\n");
            StreamWriter sw = new StreamWriter("logs.txt", true);
            sw.WriteLine(DateTime.Now + " - Connecting to Steam...");
            sw.Close();

            steamClient.Connect();


            while(isRunning)
            {
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
            Console.ReadKey();
        }
        static void OnConnected(SteamClient.ConnectedCallback callback)
        {
            Console.WriteLine("Connected to Steam... \nLogging in {0}...\n", user);
            StreamWriter sw = new StreamWriter("logs.txt", true);
            sw.WriteLine(DateTime.Now + " - Connected to Steam... Logging in {0}...", user);
            sw.Close();
            

            byte[] sentryHash = null;
            if (File.Exists("sentry.bin"))
            {
                byte[] sentryFile = File.ReadAllBytes("sentry.bin");
                sentryHash = CryptoHelper.SHAHash(sentryFile);

            }

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = user,
                Password = pass,

                AuthCode = authCode,
                TwoFactorCode = twoFactorAuth,

                SentryFileHash = sentryHash,
            });
        }

        static void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            StreamWriter sw = new StreamWriter("logs.txt", true);
            bool isSteamGuard = callback.Result == EResult.AccountLogonDenied;
            bool is2FA = callback.Result == EResult.AccountLoginDeniedNeedTwoFactor;

            if (isSteamGuard || is2FA)
            {

                Console.WriteLine("This account is SteamGuard protected.");

                if (is2FA)
                {
                    Console.Write("Please enter your 2 factor auth code from your authenticatior app: ");
                    twoFactorAuth = Console.ReadLine();
                }
                else
                {
                    Console.Write("Please enter the auth code sent to the email at {0}: ", callback.EmailDomain);
                    authCode = Console.ReadLine();
                }
               
                return;
            }
            if(callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to log in to Steam: {0}\n", callback.Result);
                sw.WriteLine($"{DateTime.Now} - Unable to log in to Steam: {0}", callback.Result);
                sw.Close();
                isRunning = false;
                return;
            }
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("{0} succesfully logged in!", user);
            Console.ResetColor();
            sw.WriteLine($"{DateTime.Now} - {user} succesfully logged in!");
            sw.Close();
        }

        static void UpdateMachineAuthCallback(SteamUser.UpdateMachineAuthCallback callback)
        {
            StreamWriter sw = new StreamWriter("logs.txt", true);

            Console.WriteLine("Updating sentry file...");
            sw.WriteLine($"{DateTime.Now} - Updating sentry file...");


            byte[] sentryHash = CryptoHelper.SHAHash(callback.Data);
            File.WriteAllBytes("sentry.bin", callback.Data);
            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,
                FileName = callback.FileName,
                BytesWritten = callback.BytesToWrite,
                FileSize = callback.Data.Length,
                Offset = callback.Offset,
                Result = EResult.OK,
                LastError = 0,
                OneTimePassword = callback.OneTimePassword,
                SentryFileHash = sentryHash,
            });

            Console.WriteLine("Done.");

            sw.WriteLine($"{DateTime.Now} - Done.");
            sw.Close();
        }

        static void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            StreamWriter sw = new StreamWriter("logs.txt", true);
            Console.WriteLine("\n{0} disconnected from Steam, reconnecting in 5...\n", user);
            sw.WriteLine(DateTime.Now + " - {0} disconnected from Steam, reconnecting in 5...", user);
            sw.Close();
            Console.ResetColor();

            Thread.Sleep(TimeSpan.FromSeconds(5));

            steamClient.Connect();
        }

        static void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            StreamWriter sw = new StreamWriter("logs.txt", true);
            Console.WriteLine("Logged off of Steam: {0}", callback.Result);
            sw.WriteLine(DateTime.Now + " - Logged off of Steam: {0}", callback.Result);
            sw.Close();
        }

        static void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            steamFriends.SetPersonaState(EPersonaState.Online);

        }

        static void OnFriendsList(SteamFriends.FriendsListCallback callback)
        {
            Thread.Sleep(200);

            foreach(var friend in callback.FriendList)
            {
                if (friend.Relationship == EFriendRelationship.RequestRecipient)
                {
                    steamFriends.AddFriend(friend.SteamID);
                    FileStream fs = new FileStream("added_friends.txt", FileMode.Create);
                    StreamWriter sw = new StreamWriter(fs, Encoding.Default);
                    Console.Write("\nNew friend added to your friend list: steamcommunity.com/profiles/");
                    Console.WriteLine(friend.SteamID);
                    sw.Write(DateTime.Now + " - New friend added to your friend list: steamcommunity.com/profiles/");
                    sw.WriteLine(friend.SteamID);
                    sw.WriteLine("");
                    Thread.Sleep(200);
                    StreamReader sr = new StreamReader("friend_welcome_message.txt");
                    steamFriends.SendChatMessage(friend.SteamID, EChatEntryType.ChatMsg, sr.ReadToEnd());
                    sw.Close();
                    fs.Close();
                }
            }
        }
        static void OnFriendAdded(SteamFriends.FriendAddedCallback callback)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            StreamWriter sw = new StreamWriter("logs.txt", true);
            Console.WriteLine("{0} is now a friend.", callback.PersonaName);
            sw.WriteLine($"{DateTime.Now} - {callback.PersonaName} is now a friend.");
            sw.Close();
            Console.ResetColor();
        }

        static void OnChatMessage(SteamFriends.FriendMsgCallback callback)
        {
            string[] args;

            StreamReader sr = new StreamReader("admin.txt");

            if (callback.EntryType == EChatEntryType.ChatMsg)
            {
                if (callback.Message.Length > 1 && sr.ReadToEnd().Contains(callback.Sender.AccountID.ToString()))
                {
                    if (callback.Message.Remove(1) == "!")
                    {
                        string command = callback.Message;
                        if (callback.Message.Contains(" "))
                        {
                            command = callback.Message.Remove(callback.Message.IndexOf(' '));
                        }

                        switch (command)
                        {
                            case "!friends":
                                Console.WriteLine("!friends command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                                for (int i = 0; i < steamFriends.GetFriendCount(); i++)
                                {
                                    SteamID friend = steamFriends.GetFriendByIndex(i);
                                    steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Friend: " + steamFriends.GetFriendPersonaName(friend) + "  State: " + steamFriends.GetFriendPersonaState(friend));
                                }
                                break;
                            case "!shutdown":
                                Console.WriteLine("\n!shutdown command recieved. User: " + steamFriends.GetFriendPersonaName(callback.Sender));
                                steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, "Bot is shutting down...");
                                Thread.Sleep(300);
                                Environment.Exit(0);
                                break;
                        }
                    }
                }
                string rLine;
                string trimmed = callback.Message;
                char[] trim = { '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '=', '+', '[', ']', '{', '}', '\\', '|', ';', ':', '"', '\'', '<', '.', '>', '/', '?' };

                for (int i = 0; i < 29; i++)
                {
                    trimmed = trimmed.Replace(trim[i].ToString(), "");
                }

                StreamReader sReader = new StreamReader("chat.txt");

                while ((rLine = sReader.ReadLine()) != null)
                {
                    string text = rLine.Remove(rLine.IndexOf('|') - 1);
                    string response = rLine.Remove(0, rLine.IndexOf('|') + 2);

                    if (callback.Message.Contains(text))
                    {
                        StreamWriter sw = new StreamWriter("logs.txt", true);
                        Console.WriteLine($"\n{text} command recieved from User: {steamFriends.GetFriendPersonaName(callback.Sender)}");
                        sw.WriteLine($"{DateTime.Now} - {text} command recieved from User: {steamFriends.GetFriendPersonaName(callback.Sender)}");
                        sw.Close();
                        steamFriends.SendChatMessage(callback.Sender, EChatEntryType.ChatMsg, response);
                        sReader.Close();
                        return;
                    }
                }

            }
        }

        public static string[] seperate(int number, char seperator, string thestring)
        {
            string[] returned = new string[4];

            int i = 0;

            int error = 0;

            int length = thestring.Length;

            foreach (char c in thestring)
            {
                if (i != number)
                {
                    if (error > length || number > 5)
                    {
                        returned[0] = "-1";
                        return returned;
                    }
                    else if (c == seperator)
                    {
                        returned[i] = thestring.Remove(thestring.IndexOf(c));
                        thestring = thestring.Remove(0, thestring.IndexOf(c) + 1);
                        i++;
                    }
                    error++;

                    if (error == length && i != number)
                    {
                        returned[0] = "-1";
                        return returned;
                    }
                }
                else
                {
                    returned[i] = thestring;
                }
            }
            return returned;





        }
    }
}
