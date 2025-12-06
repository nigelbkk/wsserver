using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;

namespace WSServer
{
[XmlRoot("Settings")]
	public class Settings
	{
		[XmlElement("ListenOn")]
		public Int32 ListenOn { get; set; }
		[XmlElement("Account")]
		public String Account { get; set; }
		[XmlElement("Password")]
		public String Password { get; set; }
		[XmlElement("AppID")]
		public String AppID { get; set; }
		[XmlElement("Cert")]
		public String Cert { get; set; }
		[XmlElement("CertPassword")]
		public String CertPassword { get; set; }
		public void Serialize()
		{
			System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(this.GetType());
			x.Serialize(Console.Out, this);
		}
		static public Settings DeSerialize()
		{
			System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(typeof(Settings));
			using (FileStream fileStream = new FileStream("settings.xml", FileMode.Open))
			{
				Settings result = (Settings) x.Deserialize(fileStream);
				return result;
			}
		}
		public static T DeserializeXMLFileToObject<T>(string XmlFilename)
		{
			T returnObject = default(T);
			if (string.IsNullOrEmpty(XmlFilename)) return default(T);

			try
			{
				StreamReader xmlStream = new StreamReader(XmlFilename);
				XmlSerializer serializer = new XmlSerializer(typeof(T));
				returnObject = (T)serializer.Deserialize(xmlStream);
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.Message);
			}
			return returnObject;
		}
	}
}
