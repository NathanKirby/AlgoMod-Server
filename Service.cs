using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlgoModPatreonServer
{
    internal class Service
    {
        /// <summary>
        /// Timed method which gets Patrons using Patreon's API, removes patrons who have canceled, adds mod credits, and caches ID list.
        /// </summary>
        /// <remarks>
        /// More documentation for each helper method available above them.
        /// <see cref="CurrentPatrons"/>
        /// <see cref="RemoveNonPatrons(string[], string)"/>
        /// <see cref="ModCredits(string)"/>
        /// </remarks>
        public static async Task CheckPatrons()
        {
            try
            {
                string path = Variables.IDSPath;
                string cachepath = Variables.IDScachePath;
                string currentPatronsraw = await CurrentPatrons();
                string currentPatronsClean = currentPatronsraw.Replace("{\"data\":[{\"attributes\":{\"email\":\"", string.Empty).Replace("\"", string.Empty).Replace("},id:", ",").Replace("last_charge_status:", string.Empty).Replace("lifetime_support_cents:", string.Empty).Replace("{attributes:{email:", string.Empty).Replace("patron_status:", string.Empty);
                string[] currentPatronsCleanSplit = currentPatronsClean.Split("],meta:{");
                string currentPatrons = currentPatronsCleanSplit[0];
                string[] patronList = currentPatrons.Split("},");

                // Returns if curl failed
                if (string.IsNullOrEmpty(currentPatronsraw))
                {
                    return;
                }

                // Removes people from list who are former patrons
                foreach (string patron in patronList)
                {
                    if (!string.IsNullOrEmpty(patron))
                    {
                        string[] patronsplit = patron.Split(",");

                        if (patronsplit[3] != "active_patron")
                        {
                            patronList = patronList.Where(s => !s.Contains(patron)).ToArray();
                        }
                    }
                }

                // Removes patrons who have unsubscribed (this must be first)
                RemoveNonPatrons(patronList, path);

                // Adds mod credits to users who have paid for another month (this must be last)
                ModCredits(cachepath);

                // Cleans up IDs file
                Variables.NewIDS = Variables.NewIDS.Replace("\n", string.Empty);
                if (Variables.NewIDS.StartsWith(","))
                {
                    Variables.NewIDS = Variables.NewIDS[1..];
                }

                // Encrypts new IDs file
                Variables.NewIDS = Encryption.EncryptIDS(Variables.NewIDS);

                // Writes new info
                File.WriteAllText(path, Variables.NewIDS);

                // Caches id list for later
                File.Delete(cachepath);
                File.WriteAllText(cachepath, Variables.NewIDS);
            }
            catch (Exception ex)
            {
                PatreonServer.Log($"!!!Error: CheckPatrons: {ex.Message}\n");
            }
        }



        /// <summary>
        /// Uses Patreon's API and access token to gather patron information.
        /// </summary>
        /// <returns>Returns list of members including their emails, liftime support in cents, last charge status, and patron status</returns>
        private static async Task<string> CurrentPatrons()
        {
            try
            {
                string accessToken = Variables.PatreonAccess;
                string url = "https://www.patreon.com/api/oauth2/v2/campaigns/9716870/members?include=&fields%5Bmember%5D=email,lifetime_support_cents,last_charge_status,patron_status&page%5Bcount%5D=1000000";

                using (HttpClient client = new())
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    HttpResponseMessage response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        PatreonServer.Log($"!!!Error: CurrentPatrons: Request failed with status code {response.StatusCode}\n");
                        return string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                PatreonServer.Log($"!!!Error: CurrentPatrons: {ex.Message}\n");
                return string.Empty;
            }
        }


        /// <summary>
        /// Compares current users in IDs file with users gathered with Patreon's API. Removes users not on the Patron.
        /// </summary>
        /// <remarks>IDs file is decrypted with <see cref="Encryption.DecryptIDS(string)"/></remarks>
        public static void RemoveNonPatrons(string[] patronlist, string path)
        {
            try
            {
                string currentPatronsraw = string.Join(",", patronlist);
                string currentIDS = Encryption.DecryptIDS(File.ReadAllText(path));
                string currentIDsTrim = currentIDS[..^1];
                string[] currentIDsSplit = currentIDsTrim.Split(",");

                // Set new content to current then remove parts in code below
                Variables.NewIDS = currentIDS;

                foreach (string line in currentIDsSplit)
                {
                    if (!string.IsNullOrEmpty(line) && line.StartsWith("USER|") && !line.StartsWith("SPECIAL|"))
                    {
                        string[] linesplit = line.Split("|");

                        // Makes sure it's a proper user id
                        if (linesplit.Length > 5)
                        {
                            string lineEmail = linesplit[3];

                            // Email in line is not a patron
                            if (!currentPatronsraw.Contains(lineEmail))
                            {
                                string[] newContent = currentIDsSplit.Where(s => !s.Contains(line)).ToArray(); /// remove line from current content, and set result to Variables.NewIDS
                                Variables.NewIDS = string.Join(",", newContent);
                                PatreonServer.Log($"RemoveNonPatrons: {lineEmail} was removed from ids.txt because they are not a Patron");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PatreonServer.Log($"!!!Error: RemoveNonPatrons: {ex.Message}\n");
            }
        }


        /// <summary>
        /// Adds mod credits to users who have paid for another month so they can get another mod
        /// </summary>
        public static void ModCredits(string idscachepath)
        {
            try
            {
                // If cache file doesn't exist, make it and return
                if (!File.Exists(idscachepath))
                {
                    File.WriteAllText(idscachepath, string.Empty);
                    return;
                }

                string[] currentidsList = Variables.NewIDS.Split(",");
                string[] idscacheList = File.ReadAllText(idscachepath).Split(",");
                string newContent = string.Empty;

                foreach (string currentid in currentidsList)
                {
                    if (string.IsNullOrEmpty(currentid) || !currentid.Contains("|") || currentid.StartsWith("SPECIAL|")) { continue; }

                    string currentidEmail = currentid.Split("|")[3];

                    // If there's no email found for this person (which shouldn't happen), cachedline gets set to "NoEmail" then ends the proccessing for this line
                    string cachedline = idscacheList.FirstOrDefault(email => email.Contains(currentidEmail)) ?? "NoEmail";
                    if (cachedline == "NoEmail")
                    {
                        continue;
                    }

                    int currentCents = int.Parse(currentid.Split("|")[6]);
                    int cacheCents = int.Parse(cachedline.Split("|")[6]);

                    // Adds 250 cents to previous to make sure they don't just tip a few cents and get a mod
                    if (currentCents > cacheCents + 250)
                    {
                        int currentCredits = int.Parse(cachedline.Split("|")[5]);
                        string newCredits = (currentCredits + 1).ToString();

                        // CLS = currentLineSplit
                        string[] cls = currentid.Split("|");

                        // If this is the first line, don't add comma
                        if (string.IsNullOrEmpty(newContent))
                        {
                            newContent = $"{cls[0]}|{cls[1]}|{cls[2]}|{cls[3]}|{cls[4]}|{newCredits}|{cls[6]}|{cls[7]}";
                        }
                        else 
                        {
                            // This is not the first line, append to text
                            string newLine = $"{cls[0]}|{cls[1]}|{cls[2]}|{cls[3]}|{cls[4]}|{newCredits}|{cls[6]}|{cls[7]}";

                            for (int i = 0; i < currentidsList.Length; i++)
                            {
                                if (currentidsList[i].Contains(currentidEmail))
                                {
                                    currentidsList[i] = newLine;
                                }
                            }

                            // List given here is edited above
                            Variables.NewIDS = string.Join(",", currentidsList);
                            PatreonServer.Log($"ModCredits: changed credits from {cls[5]} to {newCredits} for {currentid.Split("|")[3]}");
                        }
                    }
                    else
                    {
                        // If this is the first line, don't add comma
                        if (string.IsNullOrEmpty(newContent))
                        {
                            Variables.NewIDS = $"{currentid}";
                        }
                        else
                        {
                            // This is not the first line, append to text
                            for (int i = 0; i < currentidsList.Length; i++)
                            {
                                if (currentidsList[i].Contains(currentidEmail))
                                {
                                    currentidsList[i] = currentid;
                                }
                            }

                            // List given here is edited above
                            Variables.NewIDS = string.Join(",", currentidsList);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PatreonServer.Log($"!!!Error: GiveModCredits: {ex.Message}\n");
            }
        }
    }
}
