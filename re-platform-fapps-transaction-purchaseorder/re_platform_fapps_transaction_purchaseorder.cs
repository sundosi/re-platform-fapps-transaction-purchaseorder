using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Xml;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Net.Http;
using System.Net;
using System.Text;

namespace re_platform_fapps_transaction_purchaseorder
{
    public static class re_platform_fapps_transaction_purchaseorder
    {
        public static string connectionstring { get; set; }
        public static string blobcontainer { get; set; }
        public static string blobname { get; set; }


        [FunctionName("purchaseorder")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var Result = string.Empty;
            connectionstring = Environment.GetEnvironmentVariable("connectionstring");
            blobcontainer = Environment.GetEnvironmentVariable("blobcontainer");
            blobname = Environment.GetEnvironmentVariable("blobname");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                if (req.Method.ToLower() == "post")
                {
                    string replacevalue = string.Empty;
                    XDocument doc = XDocument.Parse(requestBody);

                    var elements123 = doc.Descendants().Elements().Where(a => a.Name.LocalName.ToLower() == "state".ToLower());
                    if (elements123.Count() > 0)
                    {
                        foreach (var item in elements123)
                        {
                            replacevalue = GetProcessBlobStorage(blobcontainer, blobname, item.Value).Result;
                            item.Value = replacevalue;
                        }

                        Result = Convert.ToString(doc.Declaration) + " " + Convert.ToString(doc);

                        // logic app to SAP 
                        var sapresult = post_purchaseorder_sap(Result);


                        return sapresult;


                        //return new HttpResponseMessage(HttpStatusCode.OK)
                        //{
                        //    Content = new StringContent(sapresult, Encoding.UTF8, "application/xml")
                        //};



                    }
                    else
                    {
                        Result = "mapping not found occured";
                        return new HttpResponseMessage(HttpStatusCode.BadRequest)
                        {
                            Content = new StringContent(Result, Encoding.UTF8, "application/xml")
                        };
                    }

                }
                else
                {

                    Result = "get";
                }
            }
            catch (Exception ex)
            {
                Result = "<?xml version='1.0'?><error><code>statecodenotfound</code><message>state code not found for paasing state name </message></error>";
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(Result, Encoding.UTF8, "application/xml")
                };

            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(Result, Encoding.UTF8, "application/xml")
            };
        }

        private static async Task<string> GetProcessBlobStorage(String processcode, String headerprocessfilename, string statename)
        {
            String result = null;

            //dev storage
            //string storageConnectionString = "DefaultEndpointsProtocol=https;AccountName=replatformdev;AccountKey=IOyvWlXPYcJDjiEl3arfYTp6Hc3whpSuCJMRRv5s8yyeSlm3A07UQO3bzozhoVaRhYtCLOT7NmW17yYanKnqKg==;EndpointSuffix=core.windows.net";

            string storageConnectionString = connectionstring;
            // Check whether the connection string can be parsed.
            CloudStorageAccount storageAccount;
            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                // If the connection string is valid, proceed with operations against Blob storage here.

                // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                // GET will always take from process container
                CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                // Create a container called 'quickstartblobs' and append a GUID value to it to make the name unique.
                string foldercontainer = processcode.ToLower();
                CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(foldercontainer);

                // Get a reference to the blob address, then upload the file to the blob.
                // Use the value of process code for the blob name.
                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(headerprocessfilename.ToLower()); // "-"+ Guid.NewGuid().ToString());

                try
                {
                    result = await cloudBlockBlob.DownloadTextAsync();


                    var list = JsonConvert.DeserializeObject<IEnumerable<KeyValuePair<string, string>>>(result);
                    var dictionary = list.ToDictionary(x => x.Key, x => x.Value);
                    var val = dictionary.Single(a => a.Value.ToLower() == statename.ToLower()).Key;
                    result = val;

                }

                catch (Exception ex)
                {
                    result = null;
                }

            }
            else
            {
                result = null;

            }

            return result;
        }

        public static HttpResponseMessage post_purchaseorder_sap(string saprequest)
        {
            try
            {

                using (var client = new HttpClient())
                {
                    var url = Environment.GetEnvironmentVariable("logicappurl");
                    var stringcontent = new StringContent(saprequest);
                    var result = client.PostAsync(url, stringcontent).Result;
                    if (result.IsSuccessStatusCode)
                    {
                        return result;
                    }


                    XNode node = JsonConvert.DeserializeXNode(result.Content.ReadAsStringAsync().Result);
                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent(node.ToString(), Encoding.UTF8, "application/xml")
                    };

                }

            }
            catch (Exception ex)
            {

                string Result = "<?xml version='1.0'?><error><code>SAP endpoint error </code><message> " + ex.Message + " </message></error> ";


                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(Result, Encoding.UTF8, "application/xml")
                };
            }



        }

    }
}
