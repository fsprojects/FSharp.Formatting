namespace fsdocs

open System.Collections.Concurrent
open System.IO
open System.Text

open Suave
open Suave.Filters
open Suave.Logging
open Suave.Operators
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

/// Processes and runs Suave server to host them on localhost
module Serve =
    let refreshEvent = FSharp.Control.Event<string>()

    /// generate the script to inject into html to enable hot reload during development
    let generateWatchScript (port: int) =
        let tag =
            """
<script type="text/javascript">
    var wsUri = "ws://localhost:{{PORT}}/websocket";
    function init()
    {
        websocket = new WebSocket(wsUri);
        websocket.onmessage = function(evt) {
            const data = evt.data;
            if (data.endsWith(".css")) {
                console.log(`Trying to reload ${data}`);
                const link = document.querySelector(`link[href*='${data}']`);
                if (link) {
                    const href = new URL(link.href);
                    const ticks = new Date().getTime();
                    href.searchParams.set("v", ticks);
                    link.href = href.toString();
                }
            }
            else {
                console.log('closing');
                websocket.close();
                document.location.reload();
            }
        }
    }
    window.addEventListener("load", init, false);
</script>
"""

        tag.Replace("{{PORT}}", string<int> port)

    let connectedClients = ConcurrentDictionary<WebSocket, unit>()

    let socketHandler (webSocket: WebSocket) (context: HttpContext) =
        context.runtime.logger.info (Message.eventX "New websocket connection")
        connectedClients.TryAdd(webSocket, ()) |> ignore

        socket {
            let! msg = webSocket.read ()

            match msg with
            | Close, _, _ ->
                context.runtime.logger.info (Message.eventX "Closing connection")
                connectedClients.TryRemove webSocket |> ignore
                let emptyResponse = [||] |> ByteSegment
                do! webSocket.send Close emptyResponse true
            | _ -> ()
        }

    let broadCastReload (msg: string) =
        let msg = msg |> Encoding.UTF8.GetBytes |> ByteSegment

        connectedClients.Keys
        |> Seq.map (fun client ->
            async {
                let! _ = client.send Text msg true
                ()
            })
        |> Async.Parallel
        |> Async.Ignore
        |> Async.RunSynchronously

    refreshEvent.Publish
    |> Event.add (fun fileName ->
        if Path.HasExtension fileName then
            let fileName = fileName.Replace("\\", "/").TrimEnd('~')
            broadCastReload fileName)

    let startWebServer rootOutputFolderAsGiven localPort =
        let mimeTypesMap ext =
            match ext with
            | ".323" -> Writers.createMimeType "text/h323" false
            | ".3g2" -> Writers.createMimeType "video/3gpp2" false
            | ".3gp2" -> Writers.createMimeType "video/3gpp2" false
            | ".3gp" -> Writers.createMimeType "video/3gpp" false
            | ".3gpp" -> Writers.createMimeType "video/3gpp" false
            | ".aac" -> Writers.createMimeType "audio/aac" false
            | ".aaf" -> Writers.createMimeType "application/octet-stream" false
            | ".aca" -> Writers.createMimeType "application/octet-stream" false
            | ".accdb" -> Writers.createMimeType "application/msaccess" false
            | ".accde" -> Writers.createMimeType "application/msaccess" false
            | ".accdt" -> Writers.createMimeType "application/msaccess" false
            | ".acx" -> Writers.createMimeType "application/internet-property-stream" false
            | ".adt" -> Writers.createMimeType "audio/vnd.dlna.adts" false
            | ".adts" -> Writers.createMimeType "audio/vnd.dlna.adts" false
            | ".afm" -> Writers.createMimeType "application/octet-stream" false
            | ".ai" -> Writers.createMimeType "application/postscript" false
            | ".aif" -> Writers.createMimeType "audio/x-aiff" false
            | ".aifc" -> Writers.createMimeType "audio/aiff" false
            | ".aiff" -> Writers.createMimeType "audio/aiff" false
            | ".appcache" -> Writers.createMimeType "text/cache-manifest" false
            | ".application" -> Writers.createMimeType "application/x-ms-application" false
            | ".art" -> Writers.createMimeType "image/x-jg" false
            | ".asd" -> Writers.createMimeType "application/octet-stream" false
            | ".asf" -> Writers.createMimeType "video/x-ms-asf" false
            | ".asi" -> Writers.createMimeType "application/octet-stream" false
            | ".asm" -> Writers.createMimeType "text/plain" false
            | ".asr" -> Writers.createMimeType "video/x-ms-asf" false
            | ".asx" -> Writers.createMimeType "video/x-ms-asf" false
            | ".atom" -> Writers.createMimeType "application/atom+xml" false
            | ".au" -> Writers.createMimeType "audio/basic" false
            | ".avi" -> Writers.createMimeType "video/x-msvideo" false
            | ".axs" -> Writers.createMimeType "application/olescript" false
            | ".bas" -> Writers.createMimeType "text/plain" false
            | ".bcpio" -> Writers.createMimeType "application/x-bcpio" false
            | ".bin" -> Writers.createMimeType "application/octet-stream" false
            | ".bmp" -> Writers.createMimeType "image/bmp" false
            | ".c" -> Writers.createMimeType "text/plain" false
            | ".cab" -> Writers.createMimeType "application/vnd.ms-cab-compressed" false
            | ".calx" -> Writers.createMimeType "application/vnd.ms-office.calx" false
            | ".cat" -> Writers.createMimeType "application/vnd.ms-pki.seccat" false
            | ".cdf" -> Writers.createMimeType "application/x-cdf" false
            | ".chm" -> Writers.createMimeType "application/octet-stream" false
            | ".class" -> Writers.createMimeType "application/x-java-applet" false
            | ".clp" -> Writers.createMimeType "application/x-msclip" false
            | ".cmx" -> Writers.createMimeType "image/x-cmx" false
            | ".cnf" -> Writers.createMimeType "text/plain" false
            | ".cod" -> Writers.createMimeType "image/cis-cod" false
            | ".cpio" -> Writers.createMimeType "application/x-cpio" false
            | ".cpp" -> Writers.createMimeType "text/plain" false
            | ".crd" -> Writers.createMimeType "application/x-mscardfile" false
            | ".crl" -> Writers.createMimeType "application/pkix-crl" false
            | ".crt" -> Writers.createMimeType "application/x-x509-ca-cert" false
            | ".csh" -> Writers.createMimeType "application/x-csh" false
            | ".css" -> Writers.createMimeType "text/css" false
            | ".csv" -> Writers.createMimeType "text/csv" false
            | ".cur" -> Writers.createMimeType "application/octet-stream" false
            | ".dcr" -> Writers.createMimeType "application/x-director" false
            | ".deploy" -> Writers.createMimeType "application/octet-stream" false
            | ".der" -> Writers.createMimeType "application/x-x509-ca-cert" false
            | ".dib" -> Writers.createMimeType "image/bmp" false
            | ".dir" -> Writers.createMimeType "application/x-director" false
            | ".disco" -> Writers.createMimeType "text/xml" false
            | ".dlm" -> Writers.createMimeType "text/dlm" false
            | ".doc" -> Writers.createMimeType "application/msword" false
            | ".docm" -> Writers.createMimeType "application/vnd.ms-word.document.macroEnabled.12" false
            | ".docx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.wordprocessingml.document" false
            | ".dot" -> Writers.createMimeType "application/msword" false
            | ".dotm" -> Writers.createMimeType "application/vnd.ms-word.template.macroEnabled.12" false
            | ".dotx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.wordprocessingml.template" false
            | ".dsp" -> Writers.createMimeType "application/octet-stream" false
            | ".dtd" -> Writers.createMimeType "text/xml" false
            | ".dvi" -> Writers.createMimeType "application/x-dvi" false
            | ".dvr-ms" -> Writers.createMimeType "video/x-ms-dvr" false
            | ".dwf" -> Writers.createMimeType "drawing/x-dwf" false
            | ".dwp" -> Writers.createMimeType "application/octet-stream" false
            | ".dxr" -> Writers.createMimeType "application/x-director" false
            | ".eml" -> Writers.createMimeType "message/rfc822" false
            | ".emz" -> Writers.createMimeType "application/octet-stream" false
            | ".eot" -> Writers.createMimeType "application/vnd.ms-fontobject" false
            | ".eps" -> Writers.createMimeType "application/postscript" false
            | ".etx" -> Writers.createMimeType "text/x-setext" false
            | ".evy" -> Writers.createMimeType "application/envoy" false
            | ".exe" -> Writers.createMimeType "application/vnd.microsoft.portable-executable" false
            | ".fdf" -> Writers.createMimeType "application/vnd.fdf" false
            | ".fif" -> Writers.createMimeType "application/fractals" false
            | ".fla" -> Writers.createMimeType "application/octet-stream" false
            | ".flr" -> Writers.createMimeType "x-world/x-vrml" false
            | ".flv" -> Writers.createMimeType "video/x-flv" false
            | ".gif" -> Writers.createMimeType "image/gif" false
            | ".gtar" -> Writers.createMimeType "application/x-gtar" false
            | ".gz" -> Writers.createMimeType "application/x-gzip" false
            | ".h" -> Writers.createMimeType "text/plain" false
            | ".hdf" -> Writers.createMimeType "application/x-hdf" false
            | ".hdml" -> Writers.createMimeType "text/x-hdml" false
            | ".hhc" -> Writers.createMimeType "application/x-oleobject" false
            | ".hhk" -> Writers.createMimeType "application/octet-stream" false
            | ".hhp" -> Writers.createMimeType "application/octet-stream" false
            | ".hlp" -> Writers.createMimeType "application/winhlp" false
            | ".hqx" -> Writers.createMimeType "application/mac-binhex40" false
            | ".hta" -> Writers.createMimeType "application/hta" false
            | ".htc" -> Writers.createMimeType "text/x-component" false
            | ".htm" -> Writers.createMimeType "text/html" false
            | ".html" -> Writers.createMimeType "text/html" false
            | ".htt" -> Writers.createMimeType "text/webviewhtml" false
            | ".hxt" -> Writers.createMimeType "text/html" false
            | ".ical" -> Writers.createMimeType "text/calendar" false
            | ".icalendar" -> Writers.createMimeType "text/calendar" false
            | ".ico" -> Writers.createMimeType "image/x-icon" false
            | ".ics" -> Writers.createMimeType "text/calendar" false
            | ".ief" -> Writers.createMimeType "image/ief" false
            | ".ifb" -> Writers.createMimeType "text/calendar" false
            | ".iii" -> Writers.createMimeType "application/x-iphone" false
            | ".inf" -> Writers.createMimeType "application/octet-stream" false
            | ".ins" -> Writers.createMimeType "application/x-internet-signup" false
            | ".isp" -> Writers.createMimeType "application/x-internet-signup" false
            | ".IVF" -> Writers.createMimeType "video/x-ivf" false
            | ".jar" -> Writers.createMimeType "application/java-archive" false
            | ".java" -> Writers.createMimeType "application/octet-stream" false
            | ".jck" -> Writers.createMimeType "application/liquidmotion" false
            | ".jcz" -> Writers.createMimeType "application/liquidmotion" false
            | ".jfif" -> Writers.createMimeType "image/pjpeg" false
            | ".jpb" -> Writers.createMimeType "application/octet-stream" false
            | ".jpe" -> Writers.createMimeType "image/jpeg" false
            | ".jpeg" -> Writers.createMimeType "image/jpeg" false
            | ".jpg" -> Writers.createMimeType "image/jpeg" false
            | ".js" -> Writers.createMimeType "text/javascript" false
            | ".json" -> Writers.createMimeType "application/json" false
            | ".jsx" -> Writers.createMimeType "text/jscript" false
            | ".latex" -> Writers.createMimeType "application/x-latex" false
            | ".lit" -> Writers.createMimeType "application/x-ms-reader" false
            | ".lpk" -> Writers.createMimeType "application/octet-stream" false
            | ".lsf" -> Writers.createMimeType "video/x-la-asf" false
            | ".lsx" -> Writers.createMimeType "video/x-la-asf" false
            | ".lzh" -> Writers.createMimeType "application/octet-stream" false
            | ".m13" -> Writers.createMimeType "application/x-msmediaview" false
            | ".m14" -> Writers.createMimeType "application/x-msmediaview" false
            | ".m1v" -> Writers.createMimeType "video/mpeg" false
            | ".m2ts" -> Writers.createMimeType "video/vnd.dlna.mpeg-tts" false
            | ".m3u" -> Writers.createMimeType "audio/x-mpegurl" false
            | ".m4a" -> Writers.createMimeType "audio/mp4" false
            | ".m4v" -> Writers.createMimeType "video/mp4" false
            | ".man" -> Writers.createMimeType "application/x-troff-man" false
            | ".manifest" -> Writers.createMimeType "application/x-ms-manifest" false
            | ".map" -> Writers.createMimeType "text/plain" false
            | ".markdown" -> Writers.createMimeType "text/markdown" false
            | ".md" -> Writers.createMimeType "text/markdown" false
            | ".mdb" -> Writers.createMimeType "application/x-msaccess" false
            | ".mdp" -> Writers.createMimeType "application/octet-stream" false
            | ".me" -> Writers.createMimeType "application/x-troff-me" false
            | ".mht" -> Writers.createMimeType "message/rfc822" false
            | ".mhtml" -> Writers.createMimeType "message/rfc822" false
            | ".mid" -> Writers.createMimeType "audio/mid" false
            | ".midi" -> Writers.createMimeType "audio/mid" false
            | ".mix" -> Writers.createMimeType "application/octet-stream" false
            | ".mjs" -> Writers.createMimeType "text/javascript" false
            | ".mmf" -> Writers.createMimeType "application/x-smaf" false
            | ".mno" -> Writers.createMimeType "text/xml" false
            | ".mny" -> Writers.createMimeType "application/x-msmoney" false
            | ".mov" -> Writers.createMimeType "video/quicktime" false
            | ".movie" -> Writers.createMimeType "video/x-sgi-movie" false
            | ".mp2" -> Writers.createMimeType "video/mpeg" false
            | ".mp3" -> Writers.createMimeType "audio/mpeg" false
            | ".mp4" -> Writers.createMimeType "video/mp4" false
            | ".mp4v" -> Writers.createMimeType "video/mp4" false
            | ".mpa" -> Writers.createMimeType "video/mpeg" false
            | ".mpe" -> Writers.createMimeType "video/mpeg" false
            | ".mpeg" -> Writers.createMimeType "video/mpeg" false
            | ".mpg" -> Writers.createMimeType "video/mpeg" false
            | ".mpp" -> Writers.createMimeType "application/vnd.ms-project" false
            | ".mpv2" -> Writers.createMimeType "video/mpeg" false
            | ".ms" -> Writers.createMimeType "application/x-troff-ms" false
            | ".msi" -> Writers.createMimeType "application/octet-stream" false
            | ".mso" -> Writers.createMimeType "application/octet-stream" false
            | ".mvb" -> Writers.createMimeType "application/x-msmediaview" false
            | ".mvc" -> Writers.createMimeType "application/x-miva-compiled" false
            | ".nc" -> Writers.createMimeType "application/x-netcdf" false
            | ".nsc" -> Writers.createMimeType "video/x-ms-asf" false
            | ".nws" -> Writers.createMimeType "message/rfc822" false
            | ".ocx" -> Writers.createMimeType "application/octet-stream" false
            | ".oda" -> Writers.createMimeType "application/oda" false
            | ".odc" -> Writers.createMimeType "text/x-ms-odc" false
            | ".ods" -> Writers.createMimeType "application/oleobject" false
            | ".oga" -> Writers.createMimeType "audio/ogg" false
            | ".ogg" -> Writers.createMimeType "video/ogg" false
            | ".ogv" -> Writers.createMimeType "video/ogg" false
            | ".ogx" -> Writers.createMimeType "application/ogg" false
            | ".one" -> Writers.createMimeType "application/onenote" false
            | ".onea" -> Writers.createMimeType "application/onenote" false
            | ".onetoc" -> Writers.createMimeType "application/onenote" false
            | ".onetoc2" -> Writers.createMimeType "application/onenote" false
            | ".onetmp" -> Writers.createMimeType "application/onenote" false
            | ".onepkg" -> Writers.createMimeType "application/onenote" false
            | ".osdx" -> Writers.createMimeType "application/opensearchdescription+xml" false
            | ".otf" -> Writers.createMimeType "font/otf" false
            | ".p10" -> Writers.createMimeType "application/pkcs10" false
            | ".p12" -> Writers.createMimeType "application/x-pkcs12" false
            | ".p7b" -> Writers.createMimeType "application/x-pkcs7-certificates" false
            | ".p7c" -> Writers.createMimeType "application/pkcs7-mime" false
            | ".p7m" -> Writers.createMimeType "application/pkcs7-mime" false
            | ".p7r" -> Writers.createMimeType "application/x-pkcs7-certreqresp" false
            | ".p7s" -> Writers.createMimeType "application/pkcs7-signature" false
            | ".pbm" -> Writers.createMimeType "image/x-portable-bitmap" false
            | ".pcx" -> Writers.createMimeType "application/octet-stream" false
            | ".pcz" -> Writers.createMimeType "application/octet-stream" false
            | ".pdf" -> Writers.createMimeType "application/pdf" false
            | ".pfb" -> Writers.createMimeType "application/octet-stream" false
            | ".pfm" -> Writers.createMimeType "application/octet-stream" false
            | ".pfx" -> Writers.createMimeType "application/x-pkcs12" false
            | ".pgm" -> Writers.createMimeType "image/x-portable-graymap" false
            | ".pko" -> Writers.createMimeType "application/vnd.ms-pki.pko" false
            | ".pma" -> Writers.createMimeType "application/x-perfmon" false
            | ".pmc" -> Writers.createMimeType "application/x-perfmon" false
            | ".pml" -> Writers.createMimeType "application/x-perfmon" false
            | ".pmr" -> Writers.createMimeType "application/x-perfmon" false
            | ".pmw" -> Writers.createMimeType "application/x-perfmon" false
            | ".png" -> Writers.createMimeType "image/png" false
            | ".pnm" -> Writers.createMimeType "image/x-portable-anymap" false
            | ".pnz" -> Writers.createMimeType "image/png" false
            | ".pot" -> Writers.createMimeType "application/vnd.ms-powerpoint" false
            | ".potm" -> Writers.createMimeType "application/vnd.ms-powerpoint.template.macroEnabled.12" false
            | ".potx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.presentationml.template" false
            | ".ppam" -> Writers.createMimeType "application/vnd.ms-powerpoint.addin.macroEnabled.12" false
            | ".ppm" -> Writers.createMimeType "image/x-portable-pixmap" false
            | ".pps" -> Writers.createMimeType "application/vnd.ms-powerpoint" false
            | ".ppsm" -> Writers.createMimeType "application/vnd.ms-powerpoint.slideshow.macroEnabled.12" false
            | ".ppsx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.presentationml.slideshow" false
            | ".ppt" -> Writers.createMimeType "application/vnd.ms-powerpoint" false
            | ".pptm" -> Writers.createMimeType "application/vnd.ms-powerpoint.presentation.macroEnabled.12" false
            | ".pptx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.presentationml.presentation" false
            | ".prf" -> Writers.createMimeType "application/pics-rules" false
            | ".prm" -> Writers.createMimeType "application/octet-stream" false
            | ".prx" -> Writers.createMimeType "application/octet-stream" false
            | ".ps" -> Writers.createMimeType "application/postscript" false
            | ".psd" -> Writers.createMimeType "application/octet-stream" false
            | ".psm" -> Writers.createMimeType "application/octet-stream" false
            | ".psp" -> Writers.createMimeType "application/octet-stream" false
            | ".pub" -> Writers.createMimeType "application/x-mspublisher" false
            | ".qt" -> Writers.createMimeType "video/quicktime" false
            | ".qtl" -> Writers.createMimeType "application/x-quicktimeplayer" false
            | ".qxd" -> Writers.createMimeType "application/octet-stream" false
            | ".ra" -> Writers.createMimeType "audio/x-pn-realaudio" false
            | ".ram" -> Writers.createMimeType "audio/x-pn-realaudio" false
            | ".rar" -> Writers.createMimeType "application/octet-stream" false
            | ".ras" -> Writers.createMimeType "image/x-cmu-raster" false
            | ".rf" -> Writers.createMimeType "image/vnd.rn-realflash" false
            | ".rgb" -> Writers.createMimeType "image/x-rgb" false
            | ".rm" -> Writers.createMimeType "application/vnd.rn-realmedia" false
            | ".rmi" -> Writers.createMimeType "audio/mid" false
            | ".roff" -> Writers.createMimeType "application/x-troff" false
            | ".rpm" -> Writers.createMimeType "audio/x-pn-realaudio-plugin" false
            | ".rtf" -> Writers.createMimeType "application/rtf" false
            | ".rtx" -> Writers.createMimeType "text/richtext" false
            | ".scd" -> Writers.createMimeType "application/x-msschedule" false
            | ".sct" -> Writers.createMimeType "text/scriptlet" false
            | ".sea" -> Writers.createMimeType "application/octet-stream" false
            | ".setpay" -> Writers.createMimeType "application/set-payment-initiation" false
            | ".setreg" -> Writers.createMimeType "application/set-registration-initiation" false
            | ".sgml" -> Writers.createMimeType "text/sgml" false
            | ".sh" -> Writers.createMimeType "application/x-sh" false
            | ".shar" -> Writers.createMimeType "application/x-shar" false
            | ".sit" -> Writers.createMimeType "application/x-stuffit" false
            | ".sldm" -> Writers.createMimeType "application/vnd.ms-powerpoint.slide.macroEnabled.12" false
            | ".sldx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.presentationml.slide" false
            | ".smd" -> Writers.createMimeType "audio/x-smd" false
            | ".smi" -> Writers.createMimeType "application/octet-stream" false
            | ".smx" -> Writers.createMimeType "audio/x-smd" false
            | ".smz" -> Writers.createMimeType "audio/x-smd" false
            | ".snd" -> Writers.createMimeType "audio/basic" false
            | ".snp" -> Writers.createMimeType "application/octet-stream" false
            | ".spc" -> Writers.createMimeType "application/x-pkcs7-certificates" false
            | ".spl" -> Writers.createMimeType "application/futuresplash" false
            | ".spx" -> Writers.createMimeType "audio/ogg" false
            | ".src" -> Writers.createMimeType "application/x-wais-source" false
            | ".ssm" -> Writers.createMimeType "application/streamingmedia" false
            | ".sst" -> Writers.createMimeType "application/vnd.ms-pki.certstore" false
            | ".stl" -> Writers.createMimeType "application/vnd.ms-pki.stl" false
            | ".sv4cpio" -> Writers.createMimeType "application/x-sv4cpio" false
            | ".sv4crc" -> Writers.createMimeType "application/x-sv4crc" false
            | ".svg" -> Writers.createMimeType "image/svg+xml" false
            | ".svgz" -> Writers.createMimeType "image/svg+xml" false
            | ".swf" -> Writers.createMimeType "application/x-shockwave-flash" false
            | ".t" -> Writers.createMimeType "application/x-troff" false
            | ".tar" -> Writers.createMimeType "application/x-tar" false
            | ".tcl" -> Writers.createMimeType "application/x-tcl" false
            | ".tex" -> Writers.createMimeType "application/x-tex" false
            | ".texi" -> Writers.createMimeType "application/x-texinfo" false
            | ".texinfo" -> Writers.createMimeType "application/x-texinfo" false
            | ".tgz" -> Writers.createMimeType "application/x-compressed" false
            | ".thmx" -> Writers.createMimeType "application/vnd.ms-officetheme" false
            | ".thn" -> Writers.createMimeType "application/octet-stream" false
            | ".tif" -> Writers.createMimeType "image/tiff" false
            | ".tiff" -> Writers.createMimeType "image/tiff" false
            | ".toc" -> Writers.createMimeType "application/octet-stream" false
            | ".tr" -> Writers.createMimeType "application/x-troff" false
            | ".trm" -> Writers.createMimeType "application/x-msterminal" false
            | ".ts" -> Writers.createMimeType "video/vnd.dlna.mpeg-tts" false
            | ".tsv" -> Writers.createMimeType "text/tab-separated-values" false
            | ".ttc" -> Writers.createMimeType "application/x-font-ttf" false
            | ".ttf" -> Writers.createMimeType "application/x-font-ttf" false
            | ".tts" -> Writers.createMimeType "video/vnd.dlna.mpeg-tts" false
            | ".txt" -> Writers.createMimeType "text/plain" false
            | ".u32" -> Writers.createMimeType "application/octet-stream" false
            | ".uls" -> Writers.createMimeType "text/iuls" false
            | ".ustar" -> Writers.createMimeType "application/x-ustar" false
            | ".vbs" -> Writers.createMimeType "text/vbscript" false
            | ".vcf" -> Writers.createMimeType "text/x-vcard" false
            | ".vcs" -> Writers.createMimeType "text/plain" false
            | ".vdx" -> Writers.createMimeType "application/vnd.ms-visio.viewer" false
            | ".vml" -> Writers.createMimeType "text/xml" false
            | ".vsd" -> Writers.createMimeType "application/vnd.visio" false
            | ".vss" -> Writers.createMimeType "application/vnd.visio" false
            | ".vst" -> Writers.createMimeType "application/vnd.visio" false
            | ".vsto" -> Writers.createMimeType "application/x-ms-vsto" false
            | ".vsw" -> Writers.createMimeType "application/vnd.visio" false
            | ".vsx" -> Writers.createMimeType "application/vnd.visio" false
            | ".vtx" -> Writers.createMimeType "application/vnd.visio" false
            | ".wasm" -> Writers.createMimeType "application/wasm" false
            | ".wav" -> Writers.createMimeType "audio/wav" false
            | ".wax" -> Writers.createMimeType "audio/x-ms-wax" false
            | ".wbmp" -> Writers.createMimeType "image/vnd.wap.wbmp" false
            | ".wcm" -> Writers.createMimeType "application/vnd.ms-works" false
            | ".wdb" -> Writers.createMimeType "application/vnd.ms-works" false
            | ".webm" -> Writers.createMimeType "video/webm" false
            | ".webmanifest" -> Writers.createMimeType "application/manifest+json" false
            | ".webp" -> Writers.createMimeType "image/webp" false
            | ".wks" -> Writers.createMimeType "application/vnd.ms-works" false
            | ".wm" -> Writers.createMimeType "video/x-ms-wm" false
            | ".wma" -> Writers.createMimeType "audio/x-ms-wma" false
            | ".wmd" -> Writers.createMimeType "application/x-ms-wmd" false
            | ".wmf" -> Writers.createMimeType "application/x-msmetafile" false
            | ".wml" -> Writers.createMimeType "text/vnd.wap.wml" false
            | ".wmlc" -> Writers.createMimeType "application/vnd.wap.wmlc" false
            | ".wmls" -> Writers.createMimeType "text/vnd.wap.wmlscript" false
            | ".wmlsc" -> Writers.createMimeType "application/vnd.wap.wmlscriptc" false
            | ".wmp" -> Writers.createMimeType "video/x-ms-wmp" false
            | ".wmv" -> Writers.createMimeType "video/x-ms-wmv" false
            | ".wmx" -> Writers.createMimeType "video/x-ms-wmx" false
            | ".wmz" -> Writers.createMimeType "application/x-ms-wmz" false
            | ".woff" -> Writers.createMimeType "application/font-woff" false
            | ".woff2" -> Writers.createMimeType "font/woff2" false
            | ".wps" -> Writers.createMimeType "application/vnd.ms-works" false
            | ".wri" -> Writers.createMimeType "application/x-mswrite" false
            | ".wrl" -> Writers.createMimeType "x-world/x-vrml" false
            | ".wrz" -> Writers.createMimeType "x-world/x-vrml" false
            | ".wsdl" -> Writers.createMimeType "text/xml" false
            | ".wtv" -> Writers.createMimeType "video/x-ms-wtv" false
            | ".wvx" -> Writers.createMimeType "video/x-ms-wvx" false
            | ".x" -> Writers.createMimeType "application/directx" false
            | ".xaf" -> Writers.createMimeType "x-world/x-vrml" false
            | ".xaml" -> Writers.createMimeType "application/xaml+xml" false
            | ".xap" -> Writers.createMimeType "application/x-silverlight-app" false
            | ".xbap" -> Writers.createMimeType "application/x-ms-xbap" false
            | ".xbm" -> Writers.createMimeType "image/x-xbitmap" false
            | ".xdr" -> Writers.createMimeType "text/plain" false
            | ".xht" -> Writers.createMimeType "application/xhtml+xml" false
            | ".xhtml" -> Writers.createMimeType "application/xhtml+xml" false
            | ".xla" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xlam" -> Writers.createMimeType "application/vnd.ms-excel.addin.macroEnabled.12" false
            | ".xlc" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xlm" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xls" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xlsb" -> Writers.createMimeType "application/vnd.ms-excel.sheet.binary.macroEnabled.12" false
            | ".xlsm" -> Writers.createMimeType "application/vnd.ms-excel.sheet.macroEnabled.12" false
            | ".xlsx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" false
            | ".xlt" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xltm" -> Writers.createMimeType "application/vnd.ms-excel.template.macroEnabled.12" false
            | ".xltx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.spreadsheetml.template" false
            | ".xlw" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xml" -> Writers.createMimeType "text/xml" false
            | ".xof" -> Writers.createMimeType "x-world/x-vrml" false
            | ".xpm" -> Writers.createMimeType "image/x-xpixmap" false
            | ".xps" -> Writers.createMimeType "application/vnd.ms-xpsdocument" false
            | ".xsd" -> Writers.createMimeType "text/xml" false
            | ".xsf" -> Writers.createMimeType "text/xml" false
            | ".xsl" -> Writers.createMimeType "text/xml" false
            | ".xslt" -> Writers.createMimeType "text/xml" false
            | ".xsn" -> Writers.createMimeType "application/octet-stream" false
            | ".xtp" -> Writers.createMimeType "application/octet-stream" false
            | ".xwd" -> Writers.createMimeType "image/x-xwindowdump" false
            | ".z" -> Writers.createMimeType "application/x-compress" false
            | ".zip" -> Writers.createMimeType "application/x-zip-compressed" false
            | _ -> None

        let defaultBinding = defaultConfig.bindings.[0]

        let withPort =
            { defaultBinding.socketBinding with
                port = uint16 localPort }

        let serverConfig =
            { defaultConfig with
                bindings =
                    [ { defaultBinding with
                          socketBinding = withPort } ]
                homeFolder = Some rootOutputFolderAsGiven
                mimeTypesMap = mimeTypesMap }

        let app =
            choose
                [ path "/" >=> Redirection.redirect "/index.html"
                  path "/websocket" >=> handShake socketHandler
                  Writers.setHeader "Cache-Control" "no-cache, no-store, must-revalidate"
                  >=> Writers.setHeader "Pragma" "no-cache"
                  >=> Writers.setHeader "Expires" "0"
                  >=> Files.browseHome ]

        startWebServerAsync serverConfig app |> snd |> Async.Start
