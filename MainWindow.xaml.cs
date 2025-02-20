using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using HtmlAgilityPack;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.UI;
using Windows.Storage;

namespace Scraper
{
    public sealed partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly List<UrlHistoryItem> _urlHistory = new List<UrlHistoryItem>();
        private int _currentHistoryIndex = -1;

        private async Task LoadUrlHistoryAsync()
        {
            try
            {
                string historyFilePath = Path.Combine(AppContext.BaseDirectory, "url_history.json");
                if (File.Exists(historyFilePath))
                {
                    string content = await File.ReadAllTextAsync(historyFilePath);
                    var history = System.Text.Json.JsonSerializer.Deserialize<List<UrlHistoryItem>>(content);
                    if (history != null)
                    {
                        _urlHistory.Clear();
                        _urlHistory.AddRange(history);
                    }
                }
            }
            catch (Exception)
            {
                // If there's any other error, continue with empty history
            }
        }

        private async Task SaveUrlHistoryAsync()
        {
            try
            {
                string historyFilePath = Path.Combine(AppContext.BaseDirectory, "url_history.json");
                var content = System.Text.Json.JsonSerializer.Serialize(_urlHistory, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(historyFilePath, content);
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to save URL history: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        public MainWindow()
        {
            this.InitializeComponent();
            SetDarkModeTitleBar();
            _httpClient = new HttpClient();
            // Load URL history asynchronously after window initialization
            Task.Run(async () => await LoadUrlHistoryAsync());
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () => await SaveUrlHistoryAsync()).Wait(); // Ensure history is saved before exiting
            Application.Current.Exit();
        }

        private async void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".html");
            picker.FileTypeFilter.Add(".htm");

            // Get the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var content = await Windows.Storage.FileIO.ReadTextAsync(file);
                await ProcessHtmlContent(content);
            }
        }

        private void ClearMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Clear the TreeView and URL textbox
            WebsiteTreeView.ItemsSource = null;
            UrlTextBox.Text = string.Empty;
        }

        private async void ExportMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (WebsiteTreeView.ItemsSource == null) return;

            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.FileTypeChoices.Add("JSON files", new List<string>() { ".json" });
            picker.SuggestedFileName = "webpage_structure";

            // Get the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var rootNodes = WebsiteTreeView.ItemsSource as IEnumerable<HtmlNode>;
                var jsonContent = System.Text.Json.JsonSerializer.Serialize(rootNodes, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await Windows.Storage.FileIO.WriteTextAsync(file, jsonContent);
            }
        }

        private void ExpandAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetTreeViewItemsExpansion(true);
        }

        private void CollapseAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetTreeViewItemsExpansion(false);
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "About Scraper",
                Content = "A simple web scraping tool for analyzing HTML structure.",
                CloseButtonText = "Close",
                XamlRoot = Content.XamlRoot,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Black),
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White)
            };
            _ = dialog.ShowAsync();
        }

        private async void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var settingsPage = new SettingsPage();
            var dialog = new ContentDialog
            {
                Title = "Settings",
                Content = settingsPage,
                CloseButtonText = "Close",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void UrlTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Enter:
                    ScrapeButton_Click(sender, new RoutedEventArgs());
                    break;
                case Windows.System.VirtualKey.Up:
                    if (_urlHistory.Count > 0)
                    {
                        _currentHistoryIndex = Math.Min(_currentHistoryIndex + 1, _urlHistory.Count - 1);
                        UrlTextBox.Text = _urlHistory[_currentHistoryIndex].Url;
                        UrlTextBox.SelectionStart = UrlTextBox.Text.Length;
                    }
                    break;
                case Windows.System.VirtualKey.Down:
                    if (_urlHistory.Count > 0)
                    {
                        _currentHistoryIndex = Math.Max(_currentHistoryIndex - 1, -1);
                        UrlTextBox.Text = _currentHistoryIndex >= 0 ? _urlHistory[_currentHistoryIndex].Url : string.Empty;
                        UrlTextBox.SelectionStart = UrlTextBox.Text.Length;
                    }
                    break;
            }
        }

        private void SetDarkModeTitleBar()
        {
            // Set the current window to extend content into title bar
            this.ExtendsContentIntoTitleBar = true;

            // Configure the title bar colors for dark mode
            var titleBar = this.AppWindow.TitleBar;
            titleBar.BackgroundColor = Microsoft.UI.Colors.Black;
            titleBar.ForegroundColor = Microsoft.UI.Colors.White;
            titleBar.InactiveBackgroundColor = Microsoft.UI.Colors.DarkGray;
            titleBar.InactiveForegroundColor = Microsoft.UI.Colors.LightGray;
            titleBar.ButtonBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30);
            titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 30, 30, 30);
            titleBar.ButtonInactiveForegroundColor = Microsoft.UI.Colors.Gray;
            titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 45, 45, 45);
            titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
            titleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(255, 60, 60, 60);
            titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;

            // Set larger button dimensions
            titleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Standard;
            titleBar.IconShowOptions = Microsoft.UI.Windowing.IconShowOptions.ShowIconAndSystemMenu;
        }


        private async void ScrapeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                return;
            }

            string url = UrlTextBox.Text.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
                UrlTextBox.Text = url;
            }

            // Add URL and scraping result to history
            var rootNodes = WebsiteTreeView.ItemsSource as IEnumerable<HtmlNode>;
            var historyItem = new UrlHistoryItem(url, rootNodes?.ToList());
            var existingIndex = _urlHistory.FindIndex(x => x.Url == url);
            if (existingIndex != -1)
            {
                _urlHistory.RemoveAt(existingIndex);
            }
            _urlHistory.Insert(0, historyItem);
            _currentHistoryIndex = -1;
            try
            {
                LoadingIndicator.IsActive = true;
                ScrapeButton.IsEnabled = false;
                UrlTextBox.IsEnabled = false;

                var html = await _httpClient.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var rootNode = new HtmlNode { DisplayText = "Root", Children = new ObservableCollection<HtmlNode>() };
                ProcessNode(htmlDoc.DocumentNode, rootNode);

                WebsiteTreeView.ItemsSource = new List<HtmlNode> { rootNode };
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to scrape website: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                LoadingIndicator.IsActive = false;
                ScrapeButton.IsEnabled = true;
                UrlTextBox.IsEnabled = true;
            }
        }

        private void ProcessNode(HtmlAgilityPack.HtmlNode node, HtmlNode treeNode)
        {
            foreach (var childNode in node.ChildNodes)
            {
                if (childNode.NodeType == HtmlAgilityPack.HtmlNodeType.Element)
                {
                    var displayText = $"<{childNode.Name}";
                    if (childNode.Attributes.Any())
                    {
                        displayText += $" {string.Join(" ", childNode.Attributes.Select(a => $"{a.Name}=\"{a.Value}\""))}";
                    }
                    displayText += ">";

                    if (!string.IsNullOrWhiteSpace(childNode.InnerText) && !childNode.HasChildNodes)
                    {
                        displayText += $" {childNode.InnerText.Trim()}";
                    }

                    var newNode = new HtmlNode
                    {
                        DisplayText = displayText,
                        Children = new ObservableCollection<HtmlNode>()
                    };

                    treeNode.Children.Add(newNode);

                    if (childNode.HasChildNodes)
                    {
                        ProcessNode(childNode, newNode);
                    }
                }
            }
        }

        private void WebsiteTreeView_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            var element = e.OriginalSource as FrameworkElement;
            var node = element?.DataContext as HtmlNode;

            var menuFlyout = new MenuFlyout();

            if (node != null)
            {
                var item = WebsiteTreeView.ContainerFromItem(node) as TreeViewItem;
                if (item != null)
                {
                    if (node.Children.Count == 0)
                    {
                        // For leaf nodes, show View Details option
                        var viewDetailsItem = new MenuFlyoutItem
                        {
                            Text = "View Details",
                            Icon = new SymbolIcon(Symbol.Document)
                        };
                        viewDetailsItem.Click += async (s, args) =>
                        {
                            string pythonCode = GeneratePythonScrapingCode(node.DisplayText);

                            var stackPanel = new StackPanel { Spacing = 10 };

                            stackPanel.Children.Add(new TextBlock
                            {
                                Text = "Element Details:",
                                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White)
                            });

                            stackPanel.Children.Add(new TextBlock
                            {
                                Text = node.DisplayText,
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White)
                            });

                            stackPanel.Children.Add(new TextBlock
                            {
                                Text = "Python Scraping Code:",
                                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                                Margin = new Thickness(0, 10, 0, 0),
                                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White)
                            });

                            var codeBlockGrid = new Grid();
                            var copyButton = new Button
                            {
                                Content = new SymbolIcon(Symbol.Copy),
                                Style = Application.Current.Resources["ButtonRevealStyle"] as Style,
                                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Transparent),
                                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White),
                                Margin = new Thickness(0, 5, 5, 0),
                                HorizontalAlignment = HorizontalAlignment.Right,
                                VerticalAlignment = VerticalAlignment.Top
                            };

                            copyButton.Click += async (s, args) =>
                            {
                                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                                dataPackage.SetText(pythonCode);
                                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                                // Show feedback
                                copyButton.Content = new SymbolIcon(Symbol.Accept);
                                await Task.Delay(1000);
                                copyButton.Content = new SymbolIcon(Symbol.Copy);
                            };

                            var codeBlock = new TextBlock
                            {
                                Text = pythonCode,
                                TextWrapping = TextWrapping.Wrap,
                                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LightGreen),
                                Padding = new Thickness(10)
                            };

                            var codeBlockContainer = new Border
                            {
                                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 30, 30, 30)),
                                Child = codeBlockGrid
                            };

                            codeBlockGrid.Children.Add(codeBlock);
                            codeBlockGrid.Children.Add(copyButton);
                            stackPanel.Children.Add(codeBlockContainer);

                            var dialog = new ContentDialog
                            {
                                Title = "Element Details",
                                Content = new ScrollViewer
                                {
                                    Content = stackPanel,
                                    MaxHeight = 400
                                },
                                CloseButtonText = "Close",
                                XamlRoot = Content.XamlRoot,
                                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Black),
                                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White)
                            };

                            await dialog.ShowAsync();
                        };
                        menuFlyout.Items.Add(viewDetailsItem);
                    }
                    else
                    {
                        // For non-leaf nodes, show expand/collapse option
                        var toggleItem = new MenuFlyoutItem
                        {
                            Text = item.IsExpanded ? "Collapse" : "Expand",
                            Icon = new SymbolIcon(item.IsExpanded ? Symbol.Remove : Symbol.Add)
                        };
                        toggleItem.Click += (s, args) => item.IsExpanded = !item.IsExpanded;
                        menuFlyout.Items.Add(toggleItem);
                    }
                }
            }
            else
            {
                // Add global options
                var expandAllItem = new MenuFlyoutItem
                {
                    Text = "Expand All",
                    Icon = new SymbolIcon(Symbol.Add)
                };
                expandAllItem.Click += (s, args) => SetTreeViewItemsExpansion(true);

                var collapseAllItem = new MenuFlyoutItem
                {
                    Text = "Collapse All",
                    Icon = new SymbolIcon(Symbol.Remove)
                };
                collapseAllItem.Click += (s, args) => SetTreeViewItemsExpansion(false);

                menuFlyout.Items.Add(expandAllItem);
                menuFlyout.Items.Add(collapseAllItem);
            }

            menuFlyout.ShowAt(WebsiteTreeView, e.GetPosition(WebsiteTreeView));
        }

        private void SetTreeViewItemsExpansion(bool expand)
        {
            if (WebsiteTreeView.ItemsSource is List<HtmlNode> nodes)
            {
                foreach (var node in nodes)
                {
                    SetNodeExpansion(node, expand);
                }
            }
        }

        private void SetNodeExpansion(HtmlNode node, bool expand)
        {
            var item = WebsiteTreeView.ContainerFromItem(node) as TreeViewItem;
            if (item != null)
            {
                item.IsExpanded = expand;
            }

            foreach (var child in node.Children)
            {
                SetNodeExpansion(child, expand);
            }
        }

        private async Task ProcessHtmlContent(string content)
        {
            try
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(content);

                var rootNode = new HtmlNode { DisplayText = "Root", Children = new ObservableCollection<HtmlNode>() };
                ProcessNode(htmlDoc.DocumentNode, rootNode);

                WebsiteTreeView.ItemsSource = new List<HtmlNode> { rootNode };
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to process HTML content: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private string SerializeToHtml(IEnumerable<HtmlNode> nodes)
        {
            var htmlDoc = new HtmlDocument();
            var rootNode = htmlDoc.CreateElement("html");
            htmlDoc.DocumentNode.AppendChild(rootNode);

            foreach (var node in nodes)
            {
                SerializeNodeToHtml(node, rootNode, htmlDoc);
            }

            return htmlDoc.DocumentNode.OuterHtml;
        }

        private void SerializeNodeToHtml(HtmlNode treeNode, HtmlAgilityPack.HtmlNode parentNode, HtmlDocument htmlDoc)
        {
            if (string.IsNullOrEmpty(treeNode.DisplayText)) return;

            var displayText = treeNode.DisplayText.Trim();

            // Handle text nodes
            if (!displayText.StartsWith("<"))
            {
                var textNode = htmlDoc.CreateTextNode(displayText);
                parentNode.AppendChild(textNode);
                return;
            }

            // Parse element name and attributes
            var elementInfo = displayText.Substring(1, displayText.IndexOf('>') - 1);
            var isSelfClosing = elementInfo.EndsWith("/");
            if (isSelfClosing)
            {
                elementInfo = elementInfo.TrimEnd('/');
            }

            var parts = elementInfo.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var elementName = parts[0];
            var newNode = htmlDoc.CreateElement(elementName);

            // Process attributes
            for (int i = 1; i < parts.Length; i++)
            {
                var attributeParts = parts[i].Split('=');
                if (attributeParts.Length == 2)
                {
                    var attrName = attributeParts[0];
                    var attrValue = attributeParts[1].Trim('"');
                    newNode.SetAttributeValue(attrName, attrValue);
                }
            }

            // Add text content if this is a leaf node with text
            if (!isSelfClosing && treeNode.Children.Count == 0 && displayText.Contains(">"))
            {
                var textContent = displayText.Substring(displayText.IndexOf('>') + 1).Trim();
                if (!string.IsNullOrEmpty(textContent))
                {
                    newNode.InnerHtml = textContent;
                }
            }

            parentNode.AppendChild(newNode);

            // Process child nodes only if not a self-closing tag
            if (!isSelfClosing)
            {
                foreach (var childNode in treeNode.Children)
                {
                    SerializeNodeToHtml(childNode, newNode, htmlDoc);
                }
            }
        }

        private void WebsiteTreeView_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            {
                switch (e.Key)
                {
                    case Windows.System.VirtualKey.E:
                        SetTreeViewItemsExpansion(true);
                        e.Handled = true;
                        break;
                    case Windows.System.VirtualKey.C:
                        if (!IsTextSelected())
                        {
                            SetTreeViewItemsExpansion(false);
                            e.Handled = true;
                        }
                        break;
                }
            }
        }

        private bool IsTextSelected()
        {
            var focusedElement = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement() as TextBox;
            return focusedElement != null && !string.IsNullOrEmpty(focusedElement.SelectedText);
        }

        private async void WebsiteTreeView_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement element && element.DataContext is HtmlNode node)
            {
                var item = WebsiteTreeView.ContainerFromItem(node) as TreeViewItem;
                if (item != null)
                {
                    if (node.Children.Count == 0)
                    {
                        // Generate Python scraping code
                        string pythonCode = GeneratePythonScrapingCode(node.DisplayText);

                        // Create content for the dialog
                        var stackPanel = new StackPanel { Spacing = 10 };

                        // Element details
                        stackPanel.Children.Add(new TextBlock
                        {
                            Text = "Element Details:",
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White)
                        });

                        stackPanel.Children.Add(new TextBlock
                        {
                            Text = node.DisplayText,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White)
                        });

                        // Python code
                        stackPanel.Children.Add(new TextBlock
                        {
                            Text = "Python Scraping Code:",
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                            Margin = new Thickness(0, 10, 0, 0),
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White)
                        });

                        var codeBlockGrid = new Grid();
                        var copyButton = new Button
                        {
                            Content = new SymbolIcon(Symbol.Copy),
                            Style = Application.Current.Resources["ButtonRevealStyle"] as Style,
                            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Transparent),
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White),
                            Margin = new Thickness(0, 5, 5, 0),
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Top
                        };

                        copyButton.Click += async (s, args) =>
                        {
                            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                            dataPackage.SetText(pythonCode);
                            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

                            // Show feedback
                            copyButton.Content = new SymbolIcon(Symbol.Accept);
                            await Task.Delay(1000);
                            copyButton.Content = new SymbolIcon(Symbol.Copy);
                        };

                        var codeBlock = new TextBlock
                        {
                            Text = pythonCode,
                            TextWrapping = TextWrapping.Wrap,
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.LightGreen),
                            Padding = new Thickness(10)
                        };

                        var codeBlockContainer = new Border
                        {
                            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 30, 30, 30)),
                            Child = codeBlockGrid
                        };

                        codeBlockGrid.Children.Add(codeBlock);
                        codeBlockGrid.Children.Add(copyButton);
                        stackPanel.Children.Add(codeBlockContainer);
                        var dialog = new ContentDialog
                        {
                            Title = "Element Details",
                            Content = new ScrollViewer
                            {
                                Content = stackPanel,
                                MaxHeight = 400
                            },
                            CloseButtonText = "Close",
                            XamlRoot = this.Content.XamlRoot,
                            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Black),
                            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White)
                        };
                        await dialog.ShowAsync();
                    }
                    else
                    {
                        // Toggle expansion for non-leaf nodes
                        item.IsExpanded = !item.IsExpanded;
                    }
                }
            }
        }

        private string GeneratePythonScrapingCode(string elementText)
        {
            // Parse the element text to extract tag name and attributes
            var tagStart = elementText.IndexOf('<');
            var tagEnd = elementText.IndexOf('>');
            if (tagStart == -1 || tagEnd == -1) return string.Empty;

            var tagContent = elementText.Substring(tagStart + 1, tagEnd - tagStart - 1).Trim();
            var parts = tagContent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var tagName = parts[0];

            var code = new System.Text.StringBuilder();
            code.AppendLine("from bs4 import BeautifulSoup");
            code.AppendLine("import requests");
            code.AppendLine("import pandas as pd");
            code.AppendLine("");
            code.AppendLine("def scrape_data():");
            code.AppendLine($"    # URL of the website");
            code.AppendLine($"    url = '{UrlTextBox.Text}'");
            code.AppendLine("");
            code.AppendLine("    try:");
            code.AppendLine("        # Send HTTP request with headers");
            code.AppendLine("        headers = {");
            code.AppendLine("            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36'");
            code.AppendLine("        }");
            code.AppendLine("        response = requests.get(url, headers=headers)");
            code.AppendLine("        response.raise_for_status()  # Raise an exception for bad status codes");
            code.AppendLine("");
            code.AppendLine("        # Parse the HTML content");
            code.AppendLine("        soup = BeautifulSoup(response.text, 'html.parser')");
            code.AppendLine("");
            code.AppendLine("        # Find all matching elements");

            // Generate the find method based on tag and attributes
            var findMethod = $"find_all('{tagName}'";
            var attrs = new System.Collections.Generic.List<string>();

            // Parse attributes
            for (int i = 1; i < parts.Length; i++)
            {
                var attr = parts[i];
                var equalIndex = attr.IndexOf('=');
                if (equalIndex != -1)
                {
                    var name = attr.Substring(0, equalIndex);
                    var value = attr.Substring(equalIndex + 1).Trim('"');
                    attrs.Add($"\"{name}\": \"{value}\"");
                }
            }

            if (attrs.Count > 0)
            {
                findMethod += $", attrs={{{string.Join(", ", attrs)}}}";
            }
            findMethod += ")";

            code.AppendLine($"        elements = soup.{findMethod}");
            code.AppendLine("");
            code.AppendLine("        # Extract data from elements");
            code.AppendLine("        data = []");
            code.AppendLine("        for element in elements:");
            code.AppendLine("            item = {");
            code.AppendLine("                'text': element.text.strip(),");
            code.AppendLine("                'html': str(element),");
            code.AppendLine("            }");
            code.AppendLine("            # Add all attributes");
            code.AppendLine("            item.update(element.attrs)");
            code.AppendLine("            data.append(item)");
            code.AppendLine("");
            code.AppendLine("        # Convert to DataFrame for easy handling");
            code.AppendLine("        df = pd.DataFrame(data)");
            code.AppendLine("        ");
            code.AppendLine("        # Save to CSV");
            code.AppendLine("        df.to_csv('scraped_data.csv', index=False, encoding='utf-8')");
            code.AppendLine("        print('Data has been scraped and saved to scraped_data.csv')");
            code.AppendLine("        return df");
            code.AppendLine("");
            code.AppendLine("    except Exception as e:");
            code.AppendLine("        print(f'An error occurred: {str(e)}')");
            code.AppendLine("        return None");
            code.AppendLine("");
            code.AppendLine("if __name__ == '__main__':");
            code.AppendLine("    df = scrape_data()");
            code.AppendLine("    if df is not None:");
            code.AppendLine("        print('\nFirst few rows of scraped data:')");
            code.AppendLine("        print(df.head())");

            return code.ToString();
        }
    }
}