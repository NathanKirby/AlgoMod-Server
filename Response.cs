using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlgoModPatreonServer
{
    internal class Response
    {
        /// <summary>
        /// HandleMessage determins what kind of request the client has made, calls the proper code, and returns the response code for it's action.
        /// </summary>
        /// <param name="message">This is the request from the client. Will contain '|' separated values, the first of which determins the type of response.</param>
        /// <returns>
        /// 100 = Request not recognized.
        /// 101 = Request is null.
        /// 301 = Exception reached.
        /// <see cref="USERresponse(ValueTuple{string, string, string})"/>
        /// <see cref="REQUESTresponse(ValueTuple{string, string, string})"/>
        /// <see cref="ADDIDresponse(ValueTuple{string, string, string})"/>
        /// <see cref="FORCEREMOVEresponse(ValueTuple{string, string, string})"/>
        /// <see cref="FORCEADDresponse(ValueTuple{string, string, string})"/>
        /// <see cref="INFOresponse"/>
        ///</returns>
        ///<remarks>Documentation for each response kind is available above it's respective method.</remarks>
        public static string HandleMessage(string message)
        {
            try
            {
                // Gathers passage data for response methods
                string path = Variables.IDSPath;
                if (!File.Exists(path))
                {
                    File.Create(path);
                }

                string currentContent = Encryption.DecryptIDS(File.ReadAllText(path));

                (string message, string path, string currentContent) passageData = (message, Variables.IDSPath, currentContent);

                return message switch
                {
                    // Add new user line
                    _ when message.StartsWith("USER|") => USERresponse(passageData),

                    // Request mod
                    _ when message.StartsWith("REQUEST|") => REQUESTresponse(passageData),

                    // Add ID to user line
                    _ when message.StartsWith("ADDID|") => ADDIDresponse(passageData),

                    // Moderator: Remove patron line
                    _ when message.StartsWith("FORCEREMOVE|") => FORCEREMOVEresponse(passageData),

                    // Moderator: Add special user
                    _ when message.StartsWith("SPECIAL|") => FORCEADDresponse(passageData),

                    // Sends client sensitive info
                    "INFO" => INFOresponse(),

                    // Request is null
                    string s when string.IsNullOrEmpty(s) => "101",

                    // Request not recognized
                    _ => "100"
                };
            }
            catch (Exception ex)
            {
                PatreonServer.Log($"!!!Error: HandleMessage: {ex.Message}\n");
                return "301";
            }
        }



        /// <summary>
        /// Adds new user to IDS file.
        /// </summary>
        /// <returns>
        /// 201 = Successfully added user line.
        /// 104 = User already in file, not adding.
        /// 303 = Exception reached.
        /// </returns>
        /// <remarks>Content written to IDS file is encrypted using <see cref="Encryption.EncryptIDS(string)"/></remarks>
        private static string USERresponse((string message, string path, string currentContent) passageData)
        {
            try
            {
                // User format: USER|epicid|steamid|email|tier|modcredits|centspaid|mods,
                string[] messagesplit = passageData.message.Split("|");

                // Ensures the user isn't already in the list
                if (!passageData.currentContent.Contains(messagesplit[3]))
                {
                    string newContent = string.Empty;

                    // Only adds comma if needed (safety measure)
                    if (passageData.currentContent.EndsWith(","))
                    {
                        newContent = passageData.currentContent + passageData.message;
                    }
                    else
                    {
                        newContent = passageData.currentContent + "," + passageData.message;
                    }

                    // Writes new IDS file
                    File.WriteAllText(passageData.path, Encryption.EncryptIDS(newContent));

                    PatreonServer.Log($"USER: New user added {passageData.message}");
                    return "201";
                }
                else
                {
                    return "104";
                }
            }
            catch (Exception ex)
            {
                PatreonServer.Log($"!!!Error: REQUEST: {ex.Message}\n");
                return "303";
            }
        }


        /// <summary>
        /// Allows users to spend mod credits to get new mods
        /// </summary>
        /// <returns>
        /// 202 = Successfully added new mod.
        /// 105 = User doesn't have any mod credits.
        /// 106 = User is already verified for this mod.
        /// 107 = Unable to find userline.
        /// 300 = Exception reached.
        /// </returns>
        private static string REQUESTresponse((string message, string path, string currentContent) passageData)
        {
            try
            {
                // Request format: REQUEST|user@email.com|supra
                string[] requestSplit = passageData.message.Split("|");
                string userEmail = requestSplit[1].Replace("\n", string.Empty).Replace(" ", string.Empty);
                string response = "107";
                string[] splitIDs = passageData.currentContent.Split(",");

                foreach (string line in splitIDs)
                {
                    string cleanLine = line.Replace("\n", string.Empty).Replace(" ", string.Empty);

                    if (!string.IsNullOrEmpty(cleanLine))
                    {
                        if (cleanLine.Contains(userEmail))
                        {
                            // Found line with email in the request
                            string[] lineSplit = cleanLine.Split("|");
                            int modCredits = int.Parse(lineSplit[5]);

                            if (!lineSplit[7].Contains(requestSplit[2]))
                            {
                                if (modCredits != 0)
                                {
                                    // Has credits, go through with it
                                    string newModCreditString = (modCredits - 1).ToString();
                                    string newcontent = $"{lineSplit[0]}|{lineSplit[1]}|{lineSplit[2]}|{lineSplit[3]}|{lineSplit[4]}|{newModCreditString}|{lineSplit[6]}|{lineSplit[7]}_{requestSplit[2]},";

                                    File.WriteAllText(passageData.path, Encryption.EncryptIDS(newcontent));

                                    response = "202";

                                    PatreonServer.Log($"REQUEST: Added {requestSplit[2]} to {userEmail}'s line");
                                }
                                else
                                {
                                    response = "105";
                                }
                            }
                            else
                            {
                                response = "106";
                            }

                            break;
                        }
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                PatreonServer.Log($"!!!Error: REQUEST: {ex.Message} \n");
                return "300";
            }
        }


        /// <summary>
        /// Adds an ID, Steam or Epic, to a user in the IDs file
        /// </summary>
        /// <returns>
        /// 200 = ID added to user line.
        /// 102 = Unable to determin whether adding ID is Steam or Epic.
        /// 103 = New content is empty, not adding ID.
        /// 302 = Exception reached.
        /// </returns>
        private static string ADDIDresponse((string message, string path, string currentContent) passageData)
        {
            try
            {
                // ADDID format: ADDID|user@email.com|epic or steam|ID
                string[] messageSplit = passageData.message.Split('|');
                string email = messageSplit[1];
                string type = messageSplit[2];
                string id = messageSplit[3];
                string newLine = string.Empty;
                string newContent = string.Empty;
                string[] splitIDs = passageData.currentContent.Split(",");

                // Writes new line
                foreach (string line in splitIDs)
                {
                    if (line.Contains(email))
                    {
                        string[] linesplit = line.Split("|");

                        if (type.Contains("epic"))
                        {
                            newLine = $"{linesplit[0]}|{id}|{linesplit[2]}|{linesplit[3]}|{linesplit[4]}|{linesplit[5]}|{linesplit[6]}|{linesplit[7]}";
                        }
                        else
                        {
                            if (type.Contains("steam"))
                            {
                                newLine = $"{linesplit[0]}|{linesplit[1]}|{id}|{linesplit[3]}|{linesplit[4]}|{linesplit[5]}|{linesplit[6]}|{linesplit[7]}";
                            }
                            else
                            {
                                return "102";
                            }
                        }

                        break;
                    }
                }

                // Adds new line into content
                if (!string.IsNullOrEmpty(newLine))
                {
                    foreach (string idline in splitIDs)
                    {
                        if (!string.IsNullOrEmpty(idline))
                        {
                            if (idline.Contains(email))
                            {
                                // This is the requesters line, replace it with the new line and continue
                                newContent = newContent + newLine + ",";
                                PatreonServer.Log($"ADDID: Replacing {idline} with {newLine}");
                            }
                            else
                            {
                                // This line is not the requesters line, add it and continue
                                newContent = newContent + idline + ",";
                            }
                        }
                    }
                }
                else
                {
                    return "103";
                }

                // Write new text
                File.WriteAllText(passageData.path, Encryption.EncryptIDS(newContent));
                return "200";
            }
            catch (Exception ex)
            {
                PatreonServer.Log($"!!!Error: ADDID: {ex.Message}\n");
                return "302";
            }
        }


        /// <summary>
        /// Removes a user from IDs file. This is done from the moderator application.
        /// </summary>
        /// <returns>
        /// 203 = Successfully removed user from IDs file.
        /// 304 = Exception reached.
        /// </returns>
        private static string FORCEREMOVEresponse((string message, string path, string currentContent) passageData)
        {
            // Forceremove format: FORCEREMOVE|patron email or game id
            string statusCode = string.Empty;

            try
            {
                string newIDs = string.Empty;
                string credential = passageData.message.Split("|")[1];

                string[] idsList = passageData.currentContent.Split(",");

                foreach (string idLine in idsList)
                {
                    if (string.IsNullOrEmpty(idLine))
                    {
                        continue;
                    }

                    if (!idLine.Contains(credential))
                    {
                        if (newIDs == string.Empty)
                        {
                            newIDs = idLine;
                        }
                        else
                        {
                            newIDs = $"{newIDs},{idLine}";
                        }
                    }
                }

                File.WriteAllText(passageData.path, Encryption.EncryptIDS(newIDs));
                statusCode = "203";
            }
            catch (Exception ex)
            {
                statusCode = "304";
                PatreonServer.Log($"!!!Error: Timer: {ex.Message}\n");
            }

            return statusCode;
        }


        /// <summary>
        /// Adds a special user who is exempt from timed methods and won't be removed unless manually done. This is done by the Moderator application.
        /// </summary>
        /// <returns>
        /// 204 = Successfully added special user
        /// 305 = Exception reached
        /// </returns>
        private static string FORCEADDresponse((string message, string path, string currentContent) passageData)
        {
            // FORCEADD format: SPECIAL|epicid|steamid|email|tier|modcredits|centspaid|mods
            string statusCode = string.Empty;

            try
            {
                string newIDs = string.Empty;

                if (passageData.currentContent.EndsWith(","))
                {
                    newIDs = $"{passageData.currentContent}{passageData.message}";
                }
                else
                {
                    newIDs = $"{passageData.currentContent},{passageData.message}";
                }

                File.WriteAllText(passageData.path, Encryption.EncryptIDS(newIDs));
                statusCode = "204";
            }
            catch (Exception ex)
            {
                statusCode = "305";
                PatreonServer.Log($"!!!Error: Timer: {ex.Message}\n");
            }

            return statusCode;
        }


        /// <summary>
        /// Client connects to server and asks for sensitive info like encryption and API keys
        /// </summary>
        /// <returns>Sensitive info to client</returns>
        private static string INFOresponse()
        {
            return $"SENSITIVE|{Variables.PatreonClient}|{Variables.PatreonSecret}|{Variables.MessageKey}|{Variables.MessageIV}|{Variables.IDSKey}";
        }
    }
}
