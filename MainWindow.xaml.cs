using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading.Tasks;
using CsvHelper;
using Newtonsoft.Json;
using System.Formats.Asn1;
using Newtonsoft.Json.Linq;

namespace ISBN_Processor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        private const string OpenLibraryApiUrl = "https://openlibrary.org/api/books?bibkeys={0}&format=json&jscmd=data";

        public MainWindow()
        {
            InitializeComponent();
        }

        static async Task Main(string[] args)
        {
            // Read the input file containing ISBNs (one or more ISBNs per line separated by commas)
            List<string[]> isbnRows = await ReadIsbnRowsFromFileAsync("input.txt");

            // Fetch book information for each ISBN row
            List<BookInfo> bookInfos = await FetchBookInfosAsync(isbnRows);

            // Write book information to a CSV file
            await WriteBookInfoToCsvAsync("output.csv", bookInfos);

            Console.WriteLine("Book information retrieved and saved to output.csv");
        }

        private List<string[]> ProcessFileContents(string fileContents)
        {
            List<string[]> isbnRows = new List<string[]>();

            // Split the file contents by new lines to get individual rows
            string[] rows = fileContents.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string row in rows)
            {
                // Split each row by commas
                string[] isbns = row.Split(',');

                // Remove any leading or trailing white spaces from each ISBN
                for (int i = 0; i < isbns.Length; i++)
                {
                    isbns[i] = isbns[i].Trim();
                }

                // Add the ISBNs to the list of rows
                isbnRows.Add(isbns);
            }

            return isbnRows;
        }



        public static async Task<List<string[]>> ReadIsbnRowsFromFileAsync(string filename)
        {
            List<string[]> isbnRows = new List<string[]>();

            using (StreamReader reader = new StreamReader(filename))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    string[] isbns = line.Trim().Split(',');
                    isbnRows.Add(isbns);
                }
            }

            return isbnRows;
        }

        public static async Task<List<BookInfo>> FetchBookInfosAsync(List<string[]> isbnRows)
        {
            List<BookInfo> bookInfos = new List<BookInfo>();
            Dictionary<string, dynamic> isbnCache = new Dictionary<string, dynamic>();

            using (HttpClient client = new HttpClient())
            {
                int overallRowNumber = 1;

                foreach (string[] isbns in isbnRows)
                {
                    string joinedIsbns = string.Join(",", isbns);
                    string apiUrl = string.Format(OpenLibraryApiUrl, joinedIsbns);

                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var responseData = await response.Content.ReadAsStringAsync();
                        dynamic bookData = JsonConvert.DeserializeObject(responseData);

                        int rowNumber = 1;

                        foreach (string isbn in isbns)
                        {
                            dynamic bookInfo = bookData[isbn];
                            BookInfo info = null;

                            if (isbnCache.ContainsKey(isbn))
                            {
                                dynamic cachedBookData = isbnCache[isbn];
                                dynamic cachedBookInfo = cachedBookData[isbn];

                                info = new BookInfo
                                {
                                    RowNumber = overallRowNumber,
                                    RetrievalType = DataRetrievalType.Cache,
                                    Isbn = isbn,
                                    Title = cachedBookInfo.title,
                                    Subtitle = cachedBookInfo.subtitle ?? "",
                                    AuthorNames = string.Join("; ", ((JArray)cachedBookInfo.authors).Select(a => (string)a["name"])),
                                    NumberOfPages = cachedBookInfo.number_of_pages ?? 0,
                                    PublishDate = cachedBookInfo.publish_date ?? ""
                                };
                            }
                            else
                            {
                                info = new BookInfo
                                {
                                    RowNumber = overallRowNumber,
                                    RetrievalType = DataRetrievalType.Server,
                                    Isbn = isbn,
                                    Title = bookInfo.title,
                                    Subtitle = bookInfo.subtitle ?? "",
                                    AuthorNames = string.Join("; ", ((JArray)bookInfo.authors).Select(a => (string)a["name"])),
                                    NumberOfPages = bookInfo.number_of_pages ?? 0,
                                    PublishDate = bookInfo.publish_date ?? ""
                                };

                                // Add book info to the cache
                                isbnCache[isbn] = bookData;
                            }

                            // Add book info to the list
                            if (info != null)
                            {
                                bookInfos.Add(info);
                            }

                            rowNumber++;
                        }
                    }

                    overallRowNumber++;
                }
            }

            return bookInfos;
        }


        public static async Task WriteBookInfoToCsvAsync(string filename, List<BookInfo> bookInfos)
        {
            using (StreamWriter writer = new StreamWriter(filename))
            using (CsvWriter csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture))
            {
                csv.WriteField("Row Number");
                csv.WriteField("Data Retrieval Type");
                csv.WriteField("ISBN");
                csv.WriteField("Title");
                csv.WriteField("Subtitle");
                csv.WriteField("Author Name(s)");
                csv.WriteField("Number of Pages");
                csv.WriteField("Publish Date");
                csv.NextRecord();

                foreach (BookInfo info in bookInfos)
                {
                    csv.WriteField(info.RowNumber);
                    csv.WriteField(info.RetrievalType.ToString());
                    csv.WriteField(info.Isbn);
                    csv.WriteField(!string.IsNullOrEmpty(info.Title) ? info.Title : "N/A");
                    csv.WriteField(!string.IsNullOrEmpty(info.Subtitle) ? info.Subtitle : "N/A");
                    csv.WriteField(info.AuthorNames);
                    csv.WriteField(info.NumberOfPages != 0 ? info.NumberOfPages.ToString() : "N/A");
                    csv.WriteField(!string.IsNullOrEmpty(info.PublishDate) ? info.PublishDate : "N/A");
                    csv.NextRecord();
                }
            }
        }


        private async void Button_ClickAsync(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog to allow the user to select a file
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            // Set initial directory and filter for the file types
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

            // Show the OpenFileDialog and get the result
            bool? result = openFileDialog.ShowDialog();

            // Check if the user selected a file
            if (result == true)
            {
                // Get the selected file path
                string filePath = openFileDialog.FileName;

                // Read the file contents
                string fileContents = File.ReadAllText(filePath);

                // Process the ISBNs from the file contents
                List<string[]> isbnRows = ProcessFileContents(fileContents);

                // Fetch book information for the ISBNs
                List<BookInfo> bookInfos = await FetchBookInfosAsync(isbnRows);

                // Write book information to a CSV file
                string outputFilePath = "book_info.csv";
                await WriteBookInfoToCsvAsync(outputFilePath, bookInfos);

                // Show a message box with the output file path
                MessageBox.Show("Book information saved to: " + outputFilePath, "File Processing Complete");
            }
        }
    }

    public enum DataRetrievalType
        {
            Server = 1,
            Cache = 2
        }

        public class BookInfo
        {
            public int RowNumber { get; set; }
            public DataRetrievalType RetrievalType { get; set; }
            public string Isbn { get; set; }
            public string Title { get; set; }
            public string Subtitle { get; set; }
            public string AuthorNames { get; set; }
            public int NumberOfPages { get; set; }
            public string PublishDate { get; set; }
        }

}
