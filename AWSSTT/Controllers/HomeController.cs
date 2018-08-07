using Amazon;
using Amazon.S3;
using Amazon.S3.IO;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Web;
using System.Web.Mvc;

namespace AWSSTT.Controllers
{

    public class HomeController : Controller
    {

        #region Constants

        private const string        keyName                 = "test.wav";
        private const string        fileUpload              = "FileUpload"; 

        #endregion


        public ActionResult Index()
        {
            return View();
        }



        [HttpPost]
        public string PostSoundFile()
        {         
            HttpPostedFileBase          file                        = Request.Files[fileUpload];
            Stream                      receiveStream               = file.InputStream;
            
              // basic params
           
            try
            {
                // initialize our client
                IAmazonS3               client                      = new AmazonS3Client(ConfigurationManager.AppSettings["AmazonAccessKey"], 
                                                                                        ConfigurationManager.AppSettings["AmazonSecretAccessKey"], 
                                                                                        RegionEndpoint.EUWest1);
                using (var transferUtility = new TransferUtility(client))
                {

                    var request = new TransferUtilityUploadRequest
                    {
                        BucketName = ConfigurationManager.AppSettings["AmazonBucketName"],
                        Key = keyName,
                        InputStream = receiveStream
                    };

                    transferUtility.Upload(request);
                }


            }
            catch(Exception e )
            {

            }       

            string                      transcriptURL                   = GetTranscriptURL(); 

            string                      result                          = GetTranscript(transcriptURL);

            return result;

        }


        public string GetTranscriptURL()
        {
            string                                  result                      = string.Empty; 

            try
            {
                AmazonTranscribeServiceClient       client                      = new AmazonTranscribeServiceClient(ConfigurationManager.AppSettings["AmazonAccessKey"], 
                                                                                                                    ConfigurationManager.AppSettings["AmazonSecretAccessKey"], 
                                                                                                                    RegionEndpoint.EUWest1);

                if (client != null)
                {
                    string                          url                         = String.Format(ConfigurationManager.AppSettings["AmazonBucketURL"], 
                                                                                                ConfigurationManager.AppSettings["AmazonBucketName"], 
                                                                                                keyName);
                                       
                    Media                           media                       = new Media(); 
                    StartTranscriptionJobResponse   transcriptionJobResponse    = null;
                    GetTranscriptionJobResponse     checkJob                    = null;

                    media.MediaFileUri                                          = url; 



                    StartTranscriptionJobRequest    transcriptionJobRequest     = new StartTranscriptionJobRequest();
                    string                          name                        = Guid.NewGuid().ToString(); 
                    transcriptionJobRequest.TranscriptionJobName                = name;
                    transcriptionJobRequest.MediaFormat                         = "wav"; 
                    transcriptionJobRequest.Media                               = media;
                    transcriptionJobRequest.LanguageCode                        = "en-US";

                    GetTranscriptionJobRequest      getTranscriptionJobRequest  = new GetTranscriptionJobRequest(); 
                    getTranscriptionJobRequest.TranscriptionJobName             = name;

                    bool                            finished                    = false;
                    transcriptionJobResponse                                    = client.StartTranscriptionJob(transcriptionJobRequest);

                    while (!finished)
                    {   
                        checkJob                                                = client.GetTranscriptionJob(getTranscriptionJobRequest); 

                        if(checkJob != null)
                        {
                            if(!checkJob.TranscriptionJob.TranscriptionJobStatus.Value.Contains("IN_PROGRESS"))
                            {
                                finished                                        = true; 
                            }

                            Thread.Sleep(1000);
                        }
                    }


                    result                                                      = checkJob.TranscriptionJob.Transcript.TranscriptFileUri; 
                   
                    
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return result;
        }

        public string GetTranscript(string URL)
        {
            string              result              = string.Empty; 

            using (WebClient wc = new WebClient())
            {
                var             json                = wc.DownloadString(URL);
                dynamic         transcript          = JsonConvert.DeserializeObject(json); 

                if(transcript.results != null)
                {
                    if(transcript.results.transcripts != null)
                    {
                        if(transcript.results.transcripts[0] != null)
                        {
                            result                  = transcript.results.transcripts[0].transcript;
                        }
                    }
                }
            }

            return result;
        }

    }
}
