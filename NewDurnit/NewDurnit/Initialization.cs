﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace NewDurnit
{
    public class Initialization
    {
        private string URI = "http://127.0.0.1:1234/";

        public Initialization(string address, string port)
        {
            if (address != null && port != null)
            {
                this.URI = "http://" + address + ":" + port + "/";
            }
        }

        public NameNodeInfo Start(string configXmlPath = null, long timeout = -1)
        {
            bool pathExists = false;
            NameNodeInfo nni = null;
            if (configXmlPath != null)
            {
                if (pathExists = File.Exists(configXmlPath))
                    nni = ParseInstructions(configXmlPath);
            }
            if (!pathExists)
            {
                WaitForInstructions(timeout);
            }
            return nni;
        }

        bool keepGoing = true;

        //TODO:
        public void WaitForInstructions(long timeout = -1)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(URI);
            listener.Start();
            listener.BeginGetContext(new AsyncCallback(HandleInitInstructionRequest), listener);
            while (true) { }
            //while (!HandleInitInstructionRequest(listener)) { Console.WriteLine("blah"); }
        }

        //public bool HandleInitInstructionRequest(HttpListener listener)
        //{
        //    Console.WriteLine("WAITING FOR REQUEST...");
        //    HttpListenerContext context = listener.;
        //    Console.WriteLine("GOT REQUEST!");
        //    HttpListenerRequest request = context.Request;
        //    NameValueCollection requestHeaders = context.Request.Headers;
        //    HttpListenerResponse response = context.Response;

        //    string durnitOp = requestHeaders.Get("X-DurnitOp");
        //    if (durnitOp.ToUpper() == "INIT")
        //    {
        //        Console.WriteLine("YAY!");
        //        JsonSerializer serializer = new JsonSerializer();
        //        InitInstructionModel sentInfo;
        //        using (StreamReader reader = new StreamReader(request.InputStream))
        //        using (JsonReader JsonRead = new JsonTextReader(reader))
        //        {
        //            Console.WriteLine("Getting InitInstructionModel");
        //            sentInfo = (InitInstructionModel)serializer.Deserialize(JsonRead, typeof(InitInstructionModel));
        //            Console.WriteLine("Got InitInstructionModel");

        //        }
        //        listener.Close();
        //        InitializeSelf(sentInfo);
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //    Console.WriteLine("END");
        //    return true;
        //}

        public void HandleInitInstructionRequest(IAsyncResult ar)
        {
            Console.WriteLine("Recieved request!");

            HttpListener listener = (HttpListener)ar.AsyncState;
            HttpListenerContext context = listener.EndGetContext(ar);
            HttpListenerRequest request = context.Request;
            NameValueCollection requestHeaders = context.Request.Headers;
            HttpListenerResponse response = context.Response;

            string durnitOp = requestHeaders.Get("X-DurnitOp");
            if (durnitOp.ToUpper() == "INIT")
            {
                Console.WriteLine("YAY!");
                JsonSerializer serializer = new JsonSerializer();
                InitInstructionModel sentInfo;
                using (StreamReader reader = new StreamReader(request.InputStream))
                using (JsonReader JsonRead = new JsonTextReader(reader))
                {
                    Console.WriteLine("Getting InitInstructionModel");
                    sentInfo = (InitInstructionModel)serializer.Deserialize(JsonRead, typeof(InitInstructionModel));
                    Console.WriteLine("Got InitInstructionModel");

                }
                response.StatusCode = 200;
                response.Close();
                listener.Close();
                var thread = new Thread(
                    () => InitializeSelf(sentInfo));
                thread.IsBackground = false;
                thread.Start();
            }
            else
            {
                response.StatusCode = 404;
                response.Close();
            }
            Console.WriteLine("END");
        }

        //TODO:
        public void InitializeSelf(InitInstructionModel init)
        {
            if (init.NameNodeAddress == null || init.NameNodeAddress == "")
            {
                init.NameNodeAddress = nni.Address;
                init.NameNodePort = nni.Port;
            }
            keepGoing = false;
            switch (init.Instruction)
            {
                case InitInstructions.DATANODE:
                    Console.WriteLine("I'm a datanode");
                    var dn = new DataNode(init);
                    //TODO: Start dataNode
                    break;
                case InitInstructions.NAMENODE:
                    Console.WriteLine("I'm a namenode");
                    var nn = new NameNode(init.Address, init.Port);
                    //TODO: Start nameNode
                    break;
                default:
                    Console.WriteLine("I'm dead :(");
                    break;
            }
            while (true) { }
        }


        List<InitInstructionModel> instructions = new List<InitInstructionModel>();
        InitInstructionModel myInstruction = null;

        NameNodeInfo nni = null;

        public NameNodeInfo ParseInstructions(string configXmlPath)
        {
            Stack<string> elements = new Stack<string>();

            XmlTextReader reader = new XmlTextReader(configXmlPath);

            //int currentIndent = 0;
            string lastValue = null;
            string encapsulatingElement = null;
            InitInstructionModel ins = null;
            bool identityIns = false;
            bool nameNodeKnown = false;
            //NameNodeInfo nni = null;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        elements.Push(reader.Name);
                        //for (int i = 0; i < currentIndent; i++)
                        //    Console.Write("-");
                        //currentIndent++;
                        string elem = reader.Name.ToUpper();
                        if (elem == "IDENTITY")
                        {
                            identityIns = true;
                        }
                        else if (elem == "DATANODE")
                        {
                            if (ins == null) ins = new InitInstructionModel();
                            ins.Instruction = InitInstructions.DATANODE;
                        }
                        else if (elem == "NAMENODE")
                        {
                            if (ins == null) ins = new InitInstructionModel();
                            if (nameNodeKnown) throw new SyntaxErrorException("Cannot have more than one name node in this setup");
                            nameNodeKnown = true;
                            ins.Instruction = InitInstructions.NAMENODE;
                        }
                        //Console.WriteLine(reader.Name);
                        break;
                    case XmlNodeType.Text:
                        //for (int i = 0; i < currentIndent; i++)
                        //    Console.Write("-");
                        string lastElem = elements.Peek().ToUpper();
                        if (lastElem == "ADDRESS")
                        {
                            ins.Address = reader.Value;
                        }
                        else if (lastElem == "PORT")
                        {
                            ins.Port = reader.Value;
                        }
                        //Console.WriteLine("=> " + reader.Value);
                        lastValue = reader.Value;
                        encapsulatingElement = elements.Peek();
                        break;
                    case XmlNodeType.EndElement:
                        if (elements.Pop() == reader.Name)
                        {
                            if (reader.Name.ToUpper() == "NAMENODE")
                            {
                                nameNodeKnown = true;
                                nni = new NameNodeInfo();
                                nni.Address = ins.Address;
                                nni.Port = ins.Port;
                            }
                            if (!identityIns && reader.Name.ToUpper().EndsWith("NODE"))
                            {
                                instructions.Add(ins);
                                ins = null;
                            }


                            if (reader.Name.ToUpper() == "IDENTITY" && identityIns)
                            {
                                identityIns = false;
                                if (myInstruction == null)
                                    myInstruction = ins;
                                else
                                    throw new SyntaxErrorException("Cannot set more than one instruction for self");
                                ins = null;
                            }
                            //currentIndent--;
                            //else if (reader.Name.ToUpper().StartsWith("CONFIG"))
                            //{
                            //    instructions.Add(ins);
                            //    ins = null;
                            //}
                        }
                        else
                            throw new SyntaxErrorException("Trying to end element that was not initialized: " + reader.Name);
                        break;
                }
            }

            foreach (var iim in instructions)
            {
                iim.NameNodeAddress = nni.Address;
                iim.NameNodePort = nni.Port;
            }

            var thread = new Thread(
                () => InitializeSelf(myInstruction ?? new InitInstructionModel()));
            thread.IsBackground = false;
            thread.Start();
            //InitializeSelf(myInstruction ?? new InitInstructionModel());

            SendInstructions();

            return nni;

            //Console.WriteLine("SELF: " + myInstruction.Instruction);

            //foreach (InitInstructionModel mod in instructions)
            //{
            //    Console.WriteLine(mod.Instruction);
            //}
        }

        //TODO:
        public void SendInstructions()
        {
            Console.WriteLine("Wooooooooooooooooooooork...");
            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;

            //instructions.Where(iim => iim.Instruction == InitInstructions.NAMENODE).First();

            foreach (var iim in instructions)
            {
                iim.NameNodeAddress = nni.Address;
                iim.NameNodePort = nni.Port;
                Console.WriteLine(iim.NameNodeAddress + ":" + iim.NameNodePort);
                Console.WriteLine(iim.Address + ":" + iim.Port);
                WebRequest request = WebRequest.Create("http://" + iim.Address + ":" + iim.Port + "/");
                request.Method = "POST";
                request.ContentType = "application/json";

                request.Headers.Add("X-DurnitOp", "Init");

                using (StreamWriter sw = new StreamWriter(request.GetRequestStream()))
                using (JsonWriter writer = new JsonTextWriter(sw))
                {

                    serializer.Serialize(writer, iim);

                    // {"ExpiryDate":new Date(1230375600000),"Price":0}
                }
                Console.WriteLine("about to get response");
                request.GetResponse().Close();
                Console.WriteLine("got response");
                Thread.Sleep(1000);
            }
        }
    }

    public enum InitInstructions
    {
        DATANODE,
        NAMENODE,
        NONE
    }
}
