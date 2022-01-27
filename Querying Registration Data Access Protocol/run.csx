// I WORK BRAH

#r "Newtonsoft.Json"
#r "System.Text.Json" //For Root and Json related values

using System;
using System.Text.Json.Serialization;
using System.Net;   // Needed for basically everything
using System.Text;  // For class Encoding
using System.IO;    // For StreamReader
using Newtonsoft.Json; //parse JSON 
using System.Security.Cryptography;
using System.Net.Http.Headers;

//main func - real code here
public static void Run([TimerTrigger("0 */30 * * * *")]TimerInfo myTimer, ILogger log)
{
        var access = GetDomains();
        string[] Arr = access.Split(',','"');
        
        //a very bad way of looping through the domains
        for (int j = 1; j < Arr.Length; j +=3) 
            {
                string TLD = Arr[j].Split(".")[1];
                var uri = BootStrapTLD(TLD, log);
                log.LogInformation(uri);
                RDAPResponseRoot responseRoot = null;

                if (uri != string.Empty)
                {
                    //Call the responsible RDAP server
                    responseRoot = QueryRDAP(string.Format("{0}domain/{1}", uri, Arr[j]), log);
                }
                if (responseRoot != null)
                {
                    // Store the results in LogAnalytics.
                    // Build the JSON body from the results
                    RDAPUpdate rdapUpdate = new RDAPUpdate();
                    // there are at least three "events" in the RDAP server response.  Only one of them is "interesting" to use here:  registration.
                    foreach (Event rdapEvent in responseRoot.events)
                    {
                        if (rdapEvent.eventAction == "registration")
                        {
                            // update the update object with our update
                            rdapUpdate.domainName = Arr[j];
                            rdapUpdate.registrationDate = rdapEvent.eventDate;
                            // Call the WriteData function to store the data in our LA workspace.
                            WriteData(JsonConvert.SerializeObject(rdapUpdate));
                        }
                    }
                }          
            }


/* Manually add an event

        RDAPUpdate rdapUpdate = new RDAPUpdate();
        rdapUpdate.domainName = "WINDOWS.COM";
        DateTime dt4 = new DateTime(1995, 09, 11, 6, 00, 00, DateTimeKind.Utc);
        rdapUpdate.registrationDate = dt4;
        WriteData(JsonConvert.SerializeObject(rdapUpdate));

*/     

}

//I'm using an outdated method of posting here, this is fixed later on, but I'm leaving this in as it was easier for me to understand.
private static string GetToken(){
    var request = (HttpWebRequest)WebRequest.Create("https://login.microsoftonline.com/tenantID/oauth2/token");

        var postData = "grant_type=" + Uri.EscapeDataString("client_credentials");
            postData += "&client_id=" + Uri.EscapeDataString(""); //add client ID
            postData += "&client_secret=" + Uri.EscapeDataString(""); //add client secret
            postData += "&resource=" + Uri.EscapeDataString("https://api.loganalytics.io");
        var data = Encoding.ASCII.GetBytes(postData);

        request.Method = "POST";
        request.ContentType = "application/x-www-form-urlencoded";
        request.ContentLength = data.Length;

        using (var stream = request.GetRequestStream())
        {
            stream.Write(data, 0, data.Length);
        }

        var response = (HttpWebResponse)request.GetResponse();
        var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();

        //convert token to string
        dynamic stuff = JsonConvert.DeserializeObject(responseString);
        string token = stuff.access_token;

        return token;

}
//the new way of performing POST and GET requests is used here
 public static string BootStrapTLD(string requestedTLD, ILogger log)
        {
            string queryTLD = requestedTLD;
            string responseMessage = string.Empty;
           

            //Log the request
            log.LogInformation(string.Format("BootStrapDNS function processed a request for TLD '{0}'", queryTLD));

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("https://data.iana.org/rdap/dns.json");//this can be a variable, but since it is a const I prefer not making a const value
            Root rootNode = null;
            // Add an Accept header for JSON format.
            //ok
            client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

            // Get the response from IANA
            HttpResponseMessage response = client.GetAsync("").Result;  // Blocking call!
            if (response.IsSuccessStatusCode)
            {

                var jsonString = response.Content.ReadAsStringAsync();
                jsonString.Wait();
                rootNode = JsonConvert.DeserializeObject<Root>(jsonString.Result);
                foreach (var Service in rootNode.Services)
                {
                    // Each "Service" has two nodes with multiple elements under each
                    // The first node is the TLDs
                    // The second node is the RDAP server responsible for servicing the TLDs
                    // TODO:  Really need to clean this up.
                    foreach (string TLD in Service[0])
                    {
                        if (TLD == queryTLD)
                        { // return the full server URL( server URI plus the TLD and some formatting)
                            responseMessage = string.Format("{0}",Service[1][0]);
                            break;  //break out of the foreach()
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
            }
            // Dispose of the client since all HttpClient calls are complete.
            client.Dispose();
            // return the URI
            return responseMessage;
        }
  //Simply gets the domains from the function in Sentinel
private static string GetDomains() {
    
    var access = GetToken();
    var request = (HttpWebRequest)WebRequest.Create("https://api.loganalytics.io/v1/workspaces/1ea2db20-a1e6-4d83-b869-2a9ef5033c0b/query?query=FUNCTIONNAME");
    
    request.Method = "GET";
    request.Accept = "application/json";
    request.Headers["Authorization"] = "Bearer "+access;
    

    var response = (HttpWebResponse)request.GetResponse();
    var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();

    //convert token to string
    dynamic results = JsonConvert.DeserializeObject(responseString);
    
    return results.tables[0].rows.ToString();//rows[x][0] - I'm returing this in a string format, but if you can parse it and think of a better way, please do make the change
}

public static RDAPResponseRoot QueryRDAP(string uri, ILogger log)
        {
            string responseMessage = string.Empty;


            //Log the request
            log.LogInformation(string.Format("QueryRDAP function processed a request for URI '{0}'", uri));

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(uri);
            RDAPResponseRoot rootNode = null;
            // Add an Accept header for JSON format.
            client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

            // Get the response from IANA
            HttpResponseMessage response = client.GetAsync("").Result;  // Blocking call!
            if (response.IsSuccessStatusCode)
            {

                var jsonString = response.Content.ReadAsStringAsync();
                jsonString.Wait();
                rootNode = JsonConvert.DeserializeObject<RDAPResponseRoot>(jsonString.Result);
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
            }
            // Dispose of the client since all HttpClient calls are complete.
            client.Dispose();
            // return the URI
            return rootNode;

        }

public static string BuildSignature(string message, string secret)
        {
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
            }
        }

public static void WriteData(string jsonPayload)
        {
            // Create a hash for the API signature
            var datestring = DateTime.UtcNow.ToString("r");
            var jsonBytes = Encoding.UTF8.GetBytes(jsonPayload);
            string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
            string hashedString = BuildSignature(stringToHash, "");//add hashed string
            string signature = "SharedKey " + "" + ":" + hashedString;//add shared key

            PostData(signature, datestring, jsonPayload);
        }

public static void PostData(string signature, string date, string json)
        {
            try
            {
                string url = "";//your Log analytics URL

                System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Log-Type", "ResolvedDomains_CL");
                client.DefaultRequestHeaders.Add("Authorization", signature);
                client.DefaultRequestHeaders.Add("x-ms-date", date);
                client.DefaultRequestHeaders.Add("time-generated-field", "");

                System.Net.Http.HttpContent httpContent = new StringContent(json, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                Task<System.Net.Http.HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);

                System.Net.Http.HttpContent responseContent = response.Result.Content;
                string result = responseContent.ReadAsStringAsync().Result;
                Console.WriteLine("Return Result: " + result);
            }
            catch (Exception excep)
            {
                Console.WriteLine("API Post Exception: " + excep.Message);
            }
        }

        public class RDAPUpdate
    {
        /// <summary>
        /// Gets or sets the name of the domain.
        /// </summary>
        /// <value>The name of the domain.</value>
        public string domainName { get; set; }
        /// <summary>
        /// Gets or sets the registration date.
        /// </summary>
        /// <value>The registration date.</value>
        public DateTime registrationDate { get; set; }
    }

public class Root
    {
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>The description.</value>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the publication.
        /// </summary>
        /// <value>The publication.</value>
        [JsonPropertyName("publication")]
        public DateTime Publication { get; set; }

        /// <summary>
        /// Gets or sets the services.
        /// </summary>
        /// <value>The services.</value>
        [JsonPropertyName("services")]
        public List<List<List<string>>> Services { get; set; }

        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        /// <value>The version.</value>
        [JsonPropertyName("version")]
        public string Version { get; set; }
    }

    public class RDAPResponseRoot
    {
        /// <summary>
        /// Gets or sets the name of the object class.
        /// </summary>
        /// <value>The name of the object class.</value>
        public string objectClassName { get; set; }
        /// <summary>
        /// Gets or sets the handle.
        /// </summary>
        /// <value>The handle.</value>
        public string handle { get; set; }
        /// <summary>
        /// Gets or sets the name of the LDH.
        /// </summary>
        /// <value>The name of the LDH.</value>
        public string ldhName { get; set; }
        /// <summary>
        /// Gets or sets the links.
        /// </summary>
        /// <value>The links.</value>
        public List<Link> links { get; set; }
        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        /// <value>The status.</value>
        public List<string> status { get; set; }
        /// <summary>
        /// Gets or sets the entities.
        /// </summary>
        /// <value>The entities.</value>
        public List<Entity> entities { get; set; }
        /// <summary>
        /// Gets or sets the events.
        /// </summary>
        /// <value>The events.</value>
        public List<Event> events { get; set; }
        /// <summary>
        /// Gets or sets the secure DNS.
        /// </summary>
        /// <value>The secure DNS.</value>
        public SecureDNS secureDNS { get; set; }
        /// <summary>
        /// Gets or sets the nameservers.
        /// </summary>
        /// <value>The nameservers.</value>
        public List<Nameserver> nameservers { get; set; }
        /// <summary>
        /// Gets or sets the rdap conformance.
        /// </summary>
        /// <value>The rdap conformance.</value>
        public List<string> rdapConformance { get; set; }
        /// <summary>
        /// Gets or sets the notices.
        /// </summary>
        /// <value>The notices.</value>
        public List<Notice> notices { get; set; }
    }

     public class Link
    {
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        public string value { get; set; }
        /// <summary>
        /// Gets or sets the relative.
        /// </summary>
        /// <value>The relative.</value>
        public string rel { get; set; }
        /// <summary>
        /// Gets or sets the href.
        /// </summary>
        /// <value>The href.</value>
        public string href { get; set; }
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        public string type { get; set; }
    }

     public class Entity
    {
        /// <summary>
        /// Gets or sets the name of the object class.
        /// </summary>
        /// <value>The name of the object class.</value>
        public string objectClassName { get; set; }
        /// <summary>
        /// Gets or sets the roles.
        /// </summary>
        /// <value>The roles.</value>
        public List<string> roles { get; set; }
        /// <summary>
        /// Gets or sets the vcard array.
        /// </summary>
        /// <value>The vcard array.</value>
        public List<object> vcardArray { get; set; }
        /// <summary>
        /// Gets or sets the handle.
        /// </summary>
        /// <value>The handle.</value>
        public string handle { get; set; }
        /// <summary>
        /// Gets or sets the public ids.
        /// </summary>
        /// <value>The public ids.</value>
        public List<PublicId> publicIds { get; set; }
        /// <summary>
        /// Gets or sets the entities.
        /// </summary>
        /// <value>The entities.</value>
        public List<Entity> entities { get; set; }
    }

    public class Event
    {
        /// <summary>
        /// Gets or sets the event action.
        /// </summary>
        /// <value>The event action.</value>
        public string eventAction { get; set; }
        /// <summary>
        /// Gets or sets the event date.
        /// </summary>
        /// <value>The event date.</value>
        public DateTime eventDate { get; set; }
    }

    public class SecureDNS
    {
        /// <summary>
        /// Gets or sets a value indicating whether [delegation signed].
        /// </summary>
        /// <value><c>true</c> if [delegation signed]; otherwise, <c>false</c>.</value>
        public bool delegationSigned { get; set; }
    }

    public class Nameserver
    {
        /// <summary>
        /// Gets or sets the name of the object class.
        /// </summary>
        /// <value>The name of the object class.</value>
        public string objectClassName { get; set; }
        /// <summary>
        /// Gets or sets the name of the LDH.
        /// </summary>
        /// <value>The name of the LDH.</value>
        public string ldhName { get; set; }
    }

    public class Notice
    {
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        /// <value>The title.</value>
        public string title { get; set; }
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>The description.</value>
        public List<string> description { get; set; }
        /// <summary>
        /// Gets or sets the links.
        /// </summary>
        /// <value>The links.</value>
        public List<Link> links { get; set; }
    }

    public class PublicId
    {
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        public string type { get; set; }
        /// <summary>
        /// Gets or sets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        public string identifier { get; set; }
    }
        
