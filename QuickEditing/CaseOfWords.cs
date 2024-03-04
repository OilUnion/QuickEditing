using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Task = System.Threading.Tasks.Task;

namespace QuickEditing
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class CaseOfWords
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("627c261a-1ef9-4862-8c9e-88cdfc8dc7b3");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="CaseOfWords"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private CaseOfWords(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static CaseOfWords Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in CaseOfWords's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new CaseOfWords(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private async void Execute(object sender, EventArgs e)
        {
            try
            {
                var parser = new TSql160Parser(false);
                IList<ParseError> errors;
                DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
                if (docView?.TextView is null)
                    return;

                string script = new String(docView.TextView.TextSnapshot.ToCharArray(0, docView.TextView.TextSnapshot.Length));
                TSqlFragment fragment = parser.Parse(new StringReader(script), out errors);

                var script2 = fragment as TSqlScript;

                List<String> scriptsWord = this.GetScriptsWords(fragment);

                var point = docView.TextView.Caret.Position.BufferPosition;
                int position = point.Position;

                using (var edit = docView.TextBuffer.CreateEdit(EditOptions.DefaultMinimalChange, 0, null))
                {
                    edit.Replace(0, docView.TextBuffer.CurrentSnapshot.Length, String.Join("", scriptsWord));
                    edit.Apply();
                }


                docView.TextView.Caret.MoveTo(new SnapshotPoint(docView.TextView.TextSnapshot, position));
            }
            catch (Exception ex)
            {

            }
        }

        private List<String> GetScriptsWords(TSqlFragment fragment) {
            var scriptsWord = new List<String>();

            foreach (var scriptToken in fragment.ScriptTokenStream)
            {
                var tokenType = scriptToken.TokenType.ToString().ToUpper();
                var text = scriptToken.Text?.ToUpper();

                if (tokenType == text)
                {
                    scriptsWord.Add(text);
                }
                else
                {
                    scriptsWord.Add(scriptToken.Text);
                }
            }
            return scriptsWord;
        }

    }
}
