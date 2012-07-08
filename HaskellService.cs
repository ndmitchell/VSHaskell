using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Globalization;
using System.Web.Script.Serialization;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;

namespace WellTyped.Haskell
{

    class HaskellService : LanguageService
    {
        string ghcBuildPath;

        public HaskellService(String installDir)
        {
            ghcBuildPath = installDir + "\\ghc-vs-helper.exe";
            //Trace.WriteLine(string.Format("ghcbuild.exe location: {0}", ghcBuildPath));
            if (!System.IO.File.Exists(ghcBuildPath))
            {
                throw new FileNotFoundException("Expected to find the GhcBuild.exe installed with the Haskell VS extension", ghcBuildPath);
            }
        }

        public override string Name
        {
            // Note this name must be the same name as used in the
            // ProvideLanguageService attribute of the package.
            get {
                //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering get Name() of: {0}", this.ToString()));
                return "Haskell"; 
            }
        }

        // For the moment we do not customise the language preferences at all
        // and just use the base class LanguagePreferences rather than a
        // custom derivative.
        private LanguagePreferences m_langPreferences;

        public override LanguagePreferences GetLanguagePreferences()
        {
            //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering GetLanguagePreferences() of: {0}", this.ToString()));
            if (m_langPreferences == null)
            {
                m_langPreferences = new LanguagePreferences(this.Site,
                                                            typeof(HaskellService).GUID,
                                                            this.Name);
                m_langPreferences.Init();
                m_langPreferences.LineNumbers = true;
            }
            return m_langPreferences;
        }

        public override string GetFormatFilterList()
        {
            //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering GetFormatFilterList() of: {0}", this.ToString()));
            //TODO: could add .lhs here in future...
            return "Haskell files (*.hs)\n*.hs";
        }

        private HsScanner m_scanner;

        public override IScanner GetScanner(IVsTextLines buffer)
        {
            //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering GetScanner() of: {0}", this.ToString()));
            if (m_scanner == null)
            {
                m_scanner = new HsScanner();
            }
            return m_scanner;
        }

        private IVsOutputWindow outwin;
        private IVsOutputWindowPane outpane;
        private void LogDebugMsg(String msg) {
            if (outwin == null) {
                outwin = (IVsOutputWindow)GetService(typeof(SVsOutputWindow));
            }
            outwin.GetPane(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, out outpane);
            if (outpane == null)
            {
                outwin.CreatePane(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, "Haskell package debug output", 1, 0);
                outwin.GetPane(VSConstants.OutputWindowPaneGuid.GeneralPane_guid, out outpane);
            }
            if (!msg.EndsWith("\n")) { msg = msg + "\n"; }
            outpane.OutputStringThreadSafe(msg);
        }

        public override AuthoringScope ParseSource(ParseRequest req)
        {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering ParseSource() of: {0}", this.ToString()));
            //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "ParseRequest: {0}", req.ToString()));
            //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "ParseRequest: {0}", req.Reason));
            //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "ParseRequest: {0}", req.IsSynchronous));

            Process p = new Process();
            p.StartInfo.FileName = ghcBuildPath;
            //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "ParseRequest: {0}", p.StartInfo.FileName));
            p.StartInfo.Arguments = "\"" + req.FileName + "\" --input";
            //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "ParseRequest: {0}", p.StartInfo.Arguments));
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(req.FileName);
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "ParseRequest: {0}", req.Text));
            p.StandardInput.Write(req.Text);
            p.StandardInput.Close();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            LogDebugMsg(string.Format("GhcBuild.exe helper exit code: {0}", p.ExitCode));
            LogDebugMsg(string.Format("GhcBuild.exe helper output: {0}", output));

            JavaScriptSerializer jss = new JavaScriptSerializer();
            List<Dictionary<String, String>> errs = null;
            try
            {
                errs = jss.Deserialize<List<Dictionary<String, String>>>(output);
            } catch (ArgumentException e) {
                LogDebugMsg(string.Format("Failed to parse result from GhcBuild.exe helper: {0}", e.Message));
            }
            if (errs != null) {
                foreach (var err in errs)
                {
                    //Trace.WriteLine(string.Format("kind: {0}", err["kind"]));
                    //Trace.WriteLine(string.Format("file: {0}", err["file"]));
                    //Trace.WriteLine(string.Format("startline: {0}", err["startline"]));
                    //Trace.WriteLine(string.Format("startcol: {0}", err["startcol"]));
                    //Trace.WriteLine(string.Format("endline: {0}", err["endline"]));
                    //Trace.WriteLine(string.Format("endcol: {0}", err["endcol"]));
                    //Trace.WriteLine(string.Format("message: {0}", err["message"]));

                    if (err["kind"] == "message")
                    {
                        LogDebugMsg("Error from GhcBuild.exe helper: " + err["message"]);
                    }
                    else
                    {
                        // we can use the req.Sink object to add errors etc
                        Severity severity;
                        if (err["kind"] == "Warning")
                        {
                            severity = Severity.Warning;
                        }
                        else
                        {
                            severity = Severity.Error; // note there's also "Fatal"
                        }
                        String filename = err["file"];
                        TextSpan span = new TextSpan();
                        span.iStartLine = Int32.Parse(err["startline"]) - 1;
                        span.iStartIndex = Int32.Parse(err["startcol"]) - 1;
                        span.iEndLine = Int32.Parse(err["endline"]) - 1;
                        span.iEndIndex = Int32.Parse(err["endcol"]) - 1;
                        String msg = err["message"];
                        req.Sink.AddError(filename, msg, span, severity);
                    }
                }
            }

            // TODO: we currently only care if the req.Reason is Check, meaning parse the whole file.
            // check we really can block when IsSynchronous is false.
            return new HsAuthoringScope();
            // Actually we probably don't want a single HsAuthoringScope, it's really just a data record to
            // return some of the results of the parse, and what is returned depends on the parse reason.
        }

        //public override void OnIdle(bool periodic)
        //{
        //    Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering OnIdle() of: {0}, {1}", this.ToString(), periodic));
        //    base.OnIdle(periodic);
        //}

        public override Source CreateSource(IVsTextLines buffer)
        {
            //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering CreateSource() of: {0}", this.ToString()));
            Source source = base.CreateSource(buffer);
            source.LastParseTime = 0; // This is so it'll do a parse initially upon loading.
            return source;
        }
    }
}

internal class HsScanner : IScanner
{
    private int offset;
    private string source;
    private VisualStudio.Haskell.Lexer.Lexeme[] tokens;
    private int index;
    private int state;

    public bool ScanTokenAndProvideInfoAboutIt(TokenInfo tokenInfo, ref int state)
    {
        if (tokens == null && source != null)
        {
            var st = (VisualStudio.Haskell.Lexer.LineState) state;
            tokens = VisualStudio.Haskell.Lexer.Forward(source.Substring(offset), ref st);
            source = null;
            this.state = (int) st;
        }

        if (tokens == null || index >= tokens.Length) return false;

        var t = tokens[index];
        index++;
        tokenInfo.StartIndex = t.Start + offset;
        tokenInfo.EndIndex = t.Start + offset + t.Length - 1;
        if (VisualStudio.Haskell.Lexer.IsKeyword(t.Token))
            tokenInfo.Color = TokenColor.Keyword;
        else if (VisualStudio.Haskell.Lexer.IsComment(t.Token))
            tokenInfo.Color = TokenColor.Comment;
        else if (VisualStudio.Haskell.Lexer.IsString(t.Token))
            tokenInfo.Color = TokenColor.String;
        else
            tokenInfo.Color = TokenColor.Text;
        var brack = VisualStudio.Haskell.Lexer.IsBracket(t.Token);
        tokenInfo.Type = brack ? TokenType.Delimiter : TokenType.Unknown;
        tokenInfo.Trigger = brack ? TokenTriggers.MatchBraces : TokenTriggers.None;
        if (index == tokens.Length)
            state = this.state;
        return true;
    }

    public void SetSource(string source, int offset)
    {
        this.offset = offset;
        this.source = source;
        index = 0;
        tokens = null;
    }

    public static bool UseNewScanner = true;
}

internal class HsAuthoringScope : AuthoringScope
{

    public override string GetDataTipText(int line, int col, out TextSpan span)
    {
        //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering GetDataTipText() of: {0}", this.ToString()));
        span = new TextSpan();
        return null;
    }

    public override Declarations GetDeclarations(IVsTextView view, int line, int col, TokenInfo info, ParseReason reason)
    {
        //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering GetDeclarations() of: {0}", this.ToString()));
        return null;
    }

    public override Methods GetMethods(int line, int col, string name)
    {
        //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering GetMethods() of: {0}", this.ToString()));
        return null;
    }

    public override string Goto(VSConstants.VSStd97CmdID cmd, IVsTextView textView, int line, int col, out TextSpan span)
    {
        //Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Goto() of: {0}", this.ToString()));
        span = new TextSpan();
        return null;
    }
}