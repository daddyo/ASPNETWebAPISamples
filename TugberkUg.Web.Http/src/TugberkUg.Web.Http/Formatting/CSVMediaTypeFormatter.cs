using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace TugberkUg.Web.Http.Formatting {

	public class CSVMediaTypeFormatter : System.Net.Http.Formatting.MediaTypeFormatter
	{

		public CSVMediaTypeFormatter()
		{

			SupportedMediaTypes.Add(new MediaTypeHeaderValue("text/csv"));
		}
		public CSVMediaTypeFormatter(MediaTypeMapping mediaTypeMapping)
			: this()
		{

			MediaTypeMappings.Add(mediaTypeMapping);
		}
		public CSVMediaTypeFormatter(IEnumerable<MediaTypeMapping> mediaTypeMappings)
			: this()
		{

			foreach (var mediaTypeMapping in mediaTypeMappings)
			{
				MediaTypeMappings.Add(mediaTypeMapping);
			}
		}

		protected override bool CanReadType(Type type)
		{
			if (type == null)
				throw new ArgumentNullException("type");

			return isTypeOfIEnumerable(type);
		}

		protected override bool CanWriteType(Type type)
		{
			if (type == null)
				throw new ArgumentNullException("type");

			return isTypeOfIEnumerable(type);
		}

		public override Task WriteToStreamAsync(Type type, object value, Stream stream, HttpContentHeaders contentHeaders, TransportContext transportContext)
		{
			return Task.Factory.StartNew(() =>
			{
				writeStream(type, value, stream, contentHeaders);
			});
		}


		#region private utils
		private void writeStream(Type type, object value, Stream stream, HttpContentHeaders contentHeaders)
		{

			//NOTE: We have check the type inside CanWriteType method
			//If request comes this far, the type is IEnumerable. We are safe.
			System.Diagnostics.Debug.Assert(isTypeOfIEnumerable(type));

			Type itemType = type.GetGenericArguments()[0];

			using (StreamWriter writer = new StreamWriter(stream))
			{
				try
				{
					/* Write header row to stream.
					 *	TODO: consider adding flag or sim for whether header row should be generated
					 *		Alternatively, maybe callers just start on row 1
					 */
					string headerLine = string.Join<string>(
							",", itemType.GetProperties().Select(x => x.Name)
						);
					writer.WriteLine(headerLine);


					StringBuilder entityCsv = new StringBuilder(4 * 1024);	// 4kB buffer; TODO: use static val or config'ble
					foreach (var obj in (IEnumerable<object>)value)
					{
						// Retrieve the entity values
						var vals = obj.GetType().GetProperties().Select(
							pi => new
							{
								Value = pi.GetValue(obj, null)
							}
						);

						foreach (var val in vals)
						{
							if (val.Value != null)
							{
								// TODO: consider type oriented handlers? E.g., datetimes may need special fmt'ing
								var _val = val.Value.ToString();

								// TODO: consider benefits of regex for replacements below. Neg. perf impact?
								//Check if the value contans a comma and place it in quotes if so
								if (_val.Contains(","))
									_val = string.Concat("\"", _val, "\"");
								
								//Replace any \r or \n special characters from a new line with a space
								if (_val.Contains("\r"))
									_val = _val.Replace("\r", " ");
								if (_val.Contains("\n"))
									_val = _val.Replace("\n", " ");

								entityCsv.AppendFormat("{0},", _val);
							}
							else
							{
								// Null values represented by nothing between two commas
								entityCsv.Append(",");
							}
						}

						// Now that the entity has been rendered to csv, replace the trailing comma
						// TODO: BUGBUG: if the last value was null, the trailing comma should not be removed
						writer.WriteLine(entityCsv.ToString(0, entityCsv.Length - 1));
						// Clear the string buffer prior to the next row
						entityCsv.Clear();
					}

				}
				finally
				{
					writer.Close();
				}
			}

		}

		private bool isTypeOfIEnumerable(Type type)
		{

			foreach (Type interfaceType in type.GetInterfaces())
			{

				if (interfaceType == typeof(IEnumerable))
					return true;
			}

			return false;
		}

		#endregion

	}
}