namespace fsdocs

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open System.Threading

open FSharp.Data.Adaptive
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Common
open FSharp.Formatting.Literate
open FSharp.Formatting.Templating

open Suave
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Suave.Operators
open Suave.Filters

/// Processes and runs Suave server to host them on localhost
module Serve =

    /// generate the script to inject into html to enable hot reload during development
    let generateWatchScript () =
        """
<script type="text/javascript">
    var wsUri = "ws://" + window.location.host + "/websocket";
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

    /// The mime types served for static files
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
        | ".sldx" -> Writers.createMimeType "application/vnd.openxmlformats-officedocument.presentationml.slide" false
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
        | ".xlsx" -> Writers.createMimeType "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" false
        | ".xlt" -> Writers.createMimeType "application/vnd.ms-excel" false
        | ".xltm" -> Writers.createMimeType "application/vnd.ms-excel.template.macroEnabled.12" false
        | ".xltx" -> Writers.createMimeType "application/vnd.openxmlformats-officedocument.spreadsheetml.template" false
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

    /// Start the server with the given application; the mime map is used for static files.
    let startWebServer (app: WebPart) localPort =
        let defaultBinding = defaultConfig.bindings.[0]

        let withPort =
            { defaultBinding.socketBinding with
                port = uint16 localPort
            }

        let serverConfig =
            { defaultConfig with
                bindings =
                    [
                        { defaultBinding with
                            socketBinding = withPort
                        }
                    ]
                mimeTypesMap = mimeTypesMap
            }

        // In Suave 3.x the server part of the tuple is a hot Task, no explicit start needed.
        startWebServerAsync serverConfig app |> snd |> ignore


/// The websocket clients of the browser live reload and the broadcasts to them.
type internal LiveReload() =
    let connectedClients = ConcurrentDictionary<WebSocket, unit>()

    member _.SocketHandler (webSocket: WebSocket) (_context: HttpContext) : SocketOp<unit> =
        connectedClients.TryAdd(webSocket, ()) |> ignore

        Threading.Tasks.ValueTask<Result<unit, Sockets.Error>>(
            task {
                try
                    // Block until the client sends a message or disconnects.
                    let! msg = (webSocket.read ()).AsTask()

                    match msg with
                    | Ok(Close, _, _) ->
                        let emptyResponse = [||] |> ByteSegment
                        let! _ = (webSocket.send Close emptyResponse true).AsTask()
                        ()
                    | _ -> ()
                with _ ->
                    ()

                // Deregister the client however the connection ended, so reload
                // broadcasts never touch a dead (and possibly recycled) socket.
                connectedClients.TryRemove webSocket |> ignore

                // Return Ok even when the client vanished without a close handshake,
                // otherwise Suave writes a "WebSocket disconnected" line to the console.
                return Ok()
            }
        )

    /// Send a message to every connected browser: a css file name is hot swapped, anything else reloads the page.
    member _.Broadcast(msg: string) =
        let msg = msg |> Encoding.UTF8.GetBytes |> ByteSegment

        connectedClients.Keys
        |> Seq.map (fun client ->
            async {
                try
                    let! result = (client.send Text msg true).AsTask() |> Async.AwaitTask

                    match result with
                    | Ok() -> ()
                    | Result.Error _ -> connectedClients.TryRemove client |> ignore
                with _ ->
                    // Suave 3 throws (e.g. ObjectDisposedException) when the client
                    // disconnected without a close handshake; drop the stale client.
                    connectedClients.TryRemove client |> ignore
            })
        |> Async.Parallel
        |> Async.Ignore
        |> Async.RunSynchronously

    member _.ClientCount = connectedClients.Count

/// Small additions to FSharp.Data.Adaptive.
module internal Adaptive =

    /// Like AVal.map, but the function is not re-run when the input recomputed to a structurally equal value.
    let mapCached (f: 'a -> 'b) (input: aval<'a>) : aval<'b> =
        let mutable last: ('a * 'b) option = None

        input
        |> AVal.map (fun a ->
            match last with
            | Some(la, lb) when la = a -> lb
            | _ ->
                let b = f a
                last <- Some(a, b)
                b)

    /// Read a list of adaptive values as one adaptive list.
    let ofList (xs: aval<'a> list) : aval<'a list> =
        AVal.custom (fun token -> xs |> List.map (fun x -> x.GetValue token))

/// Identity of a watched file as seen by the dependency graph.
[<Struct>]
type internal RefreshCause =
    /// The file system raised an event about this one file
    | FileEvent
    /// The periodic walk over every watched file
    | Reconciliation

[<Struct>]
type internal FileStamp =
    {
        Length: int64
        LastWriteUtc: DateTime
        /// SHA-256 of the content for files whose content feeds the graph, otherwise empty
        Hash: string
    }

/// A content page served by the site: the input file and how it is rendered for one output kind.
type internal ContentRoute =
    {
        InputFile: string
        OutputKind: OutputKind
        Template: string option
        OutputFileRelativeToRoot: string
        OutputFolderRelativeToRoot: string
        /// The folder containing the input file
        InputFolder: string
        /// The input root (as given on the command line) this file belongs to
        RootInputFolder: string
        IsOtherLang: bool
    }

/// What a URL of the site resolves to.
type internal Route =
    | ContentPage of ContentRoute
    | StaticFile of sourceFullPath: string
    | ApiPage of relativeFile: string
    | SearchIndex
    | LlmsTxt
    | LlmsFullTxt

/// Cheap facts about a content file, recomputed when the file changes.
type internal ContentMeta =
    {
        FrontMatter: ScannedFrontMatter
        /// The front matter used for next/previous links, present only when complete
        FrontMatterFile: FrontMatterFile option
        /// Full paths of the files loaded with '#load'
        Loads: string list
        /// Whether the file mentions 'cref:' and therefore needs the API model
        UsesCref: bool
        Error: string option
    }

/// The result of scanning the input trees.
type internal ScanResult =
    {
        Routes: Map<string, Route>
        FullPathFileMap: Map<(string * OutputKind), string>
        FilesWithFrontMatter: FrontMatterFile array
        NavPages: NavPage list
        TitleSources: Map<string, TitleSource>
        Skipped: (string * string) list
    }

/// The API documentation model with its page renderers, rebuilt when a project DLL changes.
[<ReferenceEquality>]
type internal ApiState =
    {
        Phased: ApiDocsPhased option
        Globals: Substitutions
        CrefResolver: string -> (string * string) option
        Pages: Map<string, string option -> Substitutions -> string>
        SearchIndex: ApiDocsSearchIndexEntry array
        Error: string option
        BuiltAt: DateTime option
    }

type internal Response =
    {
        ContentType: string
        Body: byte array
    }

type internal RenderResult =
    | Rendered of Response
    | NotFound
    | Failed of exn

type internal WatchEvent =
    {
        Time: DateTime
        Path: string
        Change: string
        Invalidated: bool
    }

type internal UrlState =
    {
        Url: string
        Valid: bool
        LastBuilt: DateTime option
        LastError: string option
    }

type internal SiteConfig =
    {
        Input: string
        /// Extra input folders (as given) and the output folder they map to
        ExtraInputs: (string * string) list
        /// The folder holding the default template, watched for changes
        DefaultTemplateFolder: string option
        Root: string
        CollectionName: string
        DefaultTemplate: string option
        DefaultMdTemplate: string option
        GenerateLlmsTxt: bool
        IgnoreUncategorized: bool
        ContentOptions: ContentOptions
        /// The project DLLs feeding the API docs
        ApiDllPaths: string list
        ApiDocsOutputKind: OutputKind
        ApiDocsTemplate: string option
        /// Generate the API docs for a (virtual) output folder, None when there are none
        GenerateApi: CrackResult -> string -> ApiDocsPhased option
        /// Re-crack the projects from disk; called when a project file changes
        Crack: unit -> CrackResult
        /// The project files and solution-wide MSBuild files that feed the crack
        ProjectFiles: string list
        WatchScript: string
        Diagnostics: Diagnostics
    }

/// The documentation site as an adaptive dependency graph: every URL is computed on first
/// request and cached until a watched file that influences it changes. No output folder is written.
type internal Site(config: SiteConfig) =
    let sep = string<char> Path.DirectorySeparatorChar
    let inputRoot = Path.GetFullPath config.Input

    let extraRoots =
        config.ExtraInputs
        |> List.map (fun (folder, rel) -> Path.GetFullPath folder, folder, rel)

    let templateFolder = config.DefaultTemplateFolder |> Option.map Path.GetFullPath
    let dllPaths = config.ApiDllPaths |> List.map Path.GetFullPath
    let dllSet = set dllPaths
    let projectPaths = config.ProjectFiles |> List.map Path.GetFullPath
    let projectSet = set projectPaths

    /// A folder that is never created: output paths are only ever used relatively.
    let virtualOutput = Path.Combine(Path.GetTempPath(), "fsdocs-watch-" + Guid.NewGuid().ToString("N"))

    /// The input trees in the order build processes them: extras first, the input last (it wins).
    let trees =
        [
            for (full, asGiven, rel) in extraRoots do
                yield full, asGiven, rel
            yield inputRoot, config.Input, "."
        ]

    let treeRoots =
        [
            yield inputRoot
            for (full, _, _) in extraRoots -> full
            match templateFolder with
            | Some t -> yield t
            | None -> ()
        ]

    let isUnder (root: string) (path: string) =
        path.StartsWith(root + sep, StringComparison.Ordinal)

    let isInDotFolder (root: string) (path: string) =
        let rel = Path.GetRelativePath(root, Path.GetDirectoryName path)

        rel <> "."
        && rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
           |> Array.exists (fun s -> s.StartsWith '.')

    let isWatchedFile (path: string) =
        dllSet.Contains path
        || projectSet.Contains path
        || treeRoots |> List.exists (fun r -> isUnder r path && not (isInDotFolder r path))

    let isMenuTemplate (path: string) =
        let name = Path.GetFileName path

        name.StartsWith("_menu", StringComparison.Ordinal)
        && name.EndsWith("_template.html", StringComparison.Ordinal)

    /// Files whose bytes feed the graph get a content hash, so byte-identical rewrites invalidate nothing.
    let contentMatters (path: string) =
        let name = Path.GetFileName path

        Content.isContentFile path
        || name.StartsWith("_template", StringComparison.Ordinal)
        || name = "_head.html"
        || name = "_body.html"
        || isMenuTemplate path
        || dllSet.Contains path
        || projectSet.Contains path

    // ---------------------------------------------------------------------------------------------
    // Inputs of the graph and the change pipeline

    let files = cmap<string, FileStamp>()
    let dlls = cmap<string, FileStamp>()
    let projects = cmap<string, FileStamp>()
    let lastStat = Dictionary<string, struct (int64 * DateTime)>()
    let knownHash = Dictionary<string, string>()
    let refreshLock = obj ()
    let renderLock = obj ()
    let events = ConcurrentQueue<WatchEvent>()
    let changedFiles = Event<string>()
    let urlStates = ConcurrentDictionary<string, UrlState>()
    let computedModels = ConcurrentQueue<string * OutputKind * DateTime>()
    let errors = ConcurrentQueue<DateTime * string>()

    let recordError (msg: string) =
        errors.Enqueue(DateTime.Now, msg)

        while errors.Count > 200 do
            errors.TryDequeue() |> ignore

    /// The content options with an error callback that also keeps the message for the doctor
    let contentOptions =
        { config.ContentOptions with
            OnError =
                fun msg ->
                    recordError msg
                    config.ContentOptions.OnError msg
        }

    let mutable modelComputations = 0
    let disposables = ResizeArray<IDisposable>()
    let mutable started = false

    let recordEvent (path: string) (change: string) (invalidated: bool) =
        // The initial population of the graph is not a file event
        if started then
            events.Enqueue
                {
                    Time = DateTime.Now
                    Path = path
                    Change = change
                    Invalidated = invalidated
                }

            while events.Count > 200 do
                events.TryDequeue() |> ignore

    let hashFile (path: string) =
        use stream = File.OpenRead path
        use sha = SHA256.Create()
        sha.ComputeHash stream |> Convert.ToHexString

    let tryStat (path: string) =
        let fi = FileInfo path

        if fi.Exists then
            Some(struct (fi.Length, fi.LastWriteTimeUtc))
        else
            None

    /// Bring the graph up to date with one file. Returns true when something was invalidated.
    /// Reconciliation visits every watched file, so hashing all of them every couple of seconds
    /// would be wasteful and an unchanged (length, last write time) is taken to mean an unchanged
    /// file there. A file event names one file, so it hashes that file even when the stat is
    /// unchanged: two writes of the same length can share a last write time, on Windows the
    /// granularity of the file time is well above the time it takes to write a file twice. Files
    /// whose content is not hashed keep the stat check either way, so a repeated event (the file
    /// system raises several per save) does not invalidate them twice.
    let rec refresh (cause: RefreshCause) (change: string) (pathAsGiven: string) : bool =
        lock refreshLock (fun () ->
            let path = Path.GetFullPath pathAsGiven

            if Directory.Exists path then
                let underRoots = treeRoots |> List.filter (fun r -> isUnder r path || r = path)

                if underRoots.IsEmpty then
                    false
                else
                    let mutable any = false

                    for f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories) do
                        if refresh cause change f then
                            any <- true

                    // Files that were under this folder and are now gone
                    for f in lastStat.Keys |> Seq.filter (isUnder path) |> Seq.toList do
                        if refresh cause "deleted" f then
                            any <- true

                    any
            elif not (isWatchedFile path) then
                false
            else
                let map =
                    if dllSet.Contains path then dlls
                    elif projectSet.Contains path then projects
                    else files

                match tryStat path with
                | None ->
                    if lastStat.Remove path then
                        knownHash.Remove path |> ignore
                        transact (fun () -> map.Remove path |> ignore)
                        recordEvent path "deleted" true
                        true
                    else
                        // A folder vanished together with its files, they were removed above
                        transact (fun () -> map.Remove path |> ignore)
                        false
                | Some(struct (length, lastWrite) as stat) ->
                    match lastStat.TryGetValue path with
                    | true, previous when previous = stat && (cause = Reconciliation || not (contentMatters path)) ->
                        false
                    | _ ->
                        let hashResult =
                            if contentMatters path then
                                try
                                    Result.Ok(hashFile path)
                                with ex ->
                                    Result.Error ex
                            else
                                Result.Ok ""

                        match hashResult with
                        | Result.Error _ ->
                            // Mid-write or locked: leave the stat alone so the reconciler retries
                            false
                        | Result.Ok hash ->
                            lastStat.[path] <- stat

                            match knownHash.TryGetValue path with
                            | true, known when contentMatters path && known = hash ->
                                recordEvent path change false
                                false
                            | _ ->
                                knownHash.[path] <- hash

                                transact (fun () ->
                                    map.[path] <-
                                        {
                                            Length = length
                                            LastWriteUtc = lastWrite
                                            Hash = hash
                                        })

                                recordEvent path change true
                                changedFiles.Trigger path
                                true)

    /// Forget the current bytes of a file the site rewrote itself (notebook evaluation), so the
    /// rewrite does not invalidate the page that was just computed from it.
    let markSelfWrite (path: string) =
        lock refreshLock (fun () ->
            let path = Path.GetFullPath path

            match tryStat path with
            | Some stat ->
                lastStat.[path] <- stat

                if contentMatters path then
                    try
                        knownHash.[path] <- hashFile path
                    with _ ->
                        ()
            | None -> ())

    let walkRoots () =
        [
            for root in treeRoots do
                if Directory.Exists root then
                    for f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories) do
                        if not (isInDotFolder root f) then
                            yield f
            for dll in dllPaths do
                if File.Exists dll then
                    yield dll
            for project in projectPaths do
                if File.Exists project then
                    yield project
        ]

    /// Compare the watched roots with the last snapshot and refresh every difference.
    let reconcile () =
        lock refreshLock (fun () ->
            let current = System.Collections.Generic.HashSet<string>(walkRoots ())

            for f in current do
                refresh Reconciliation "changed" f |> ignore

            for f in lastStat.Keys |> Seq.filter (current.Contains >> not) |> Seq.toList do
                refresh Reconciliation "deleted" f |> ignore)

    // ---------------------------------------------------------------------------------------------
    // Derived nodes

    let loadRegex = Regex(@"^\s*#load\s+(.*)$", RegexOptions.Multiline)
    let quotedRegex = Regex("\"([^\"]+)\"")

    let loadsOf (path: string) (text: string) =
        if Content.isFsxFile path then
            [
                for m in loadRegex.Matches text do
                    for q in quotedRegex.Matches m.Groups.[1].Value do
                        Path.GetFullPath(Path.Combine(Path.GetDirectoryName path, q.Groups.[1].Value))
            ]
        else
            []

    let computeMeta (path: string) : ContentMeta =
        try
            let text = File.ReadAllText path

            {
                FrontMatter = Content.scanFrontMatter path
                FrontMatterFile = Content.parseFrontMatterOfFile path
                Loads = loadsOf path text
                UsesCref = text.Contains "cref:"
                Error = None
            }
        with ex ->
            {
                FrontMatter =
                    {
                        Title = Path.GetFileNameWithoutExtension path
                        TitleSource = TitleSource.FileName
                        Category = None
                        CategoryIndex = None
                        Index = None
                    }
                FrontMatterFile = None
                Loads = []
                UsesCref = true
                Error = Some ex.Message
            }

    let urlOf (outputFileRelativeToRoot: string) =
        let u = outputFileRelativeToRoot.Replace("\\", "/")

        let u =
            if u.StartsWith("./", StringComparison.Ordinal) then
                u.[2..]
            else
                u

        "/" + u.TrimStart('/')

    /// The nearest template of the given name from the file's folder up to the root of its tree.
    let findTemplate
        (paths: System.Collections.Generic.HashSet<string>)
        (root: string)
        (folder: string)
        (name: string)
        =
        let rec go (dir: string) =
            let candidate = Path.Combine(dir, name)

            if paths.Contains candidate then Some candidate
            elif dir = root || dir.Length <= root.Length then None
            else go (Path.GetDirectoryName dir)

        go folder

    /// Build treats every folder from the tree root down to the file as a possible language folder.
    let isOtherLang (root: string) (folder: string) =
        let rel = Path.GetRelativePath(root, folder)

        [
            yield Path.GetFileName root
            if rel <> "." then
                yield! rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        ]
        |> List.exists Content.isOtherLangFolderName

    let allKinds = [ OutputKind.Html; OutputKind.Latex; OutputKind.Pynb; OutputKind.Fsx; OutputKind.Markdown ]

    let computeScan (paths: string list) (metas: Map<string, ContentMeta>) : ScanResult =
        let pathSet = System.Collections.Generic.HashSet<string>(paths)
        let skipped = ResizeArray<string * string>()
        let routes = ResizeArray<string * Route>()
        let fileMap = ResizeArray<(string * OutputKind) * string>()
        let navPages = ResizeArray<NavPage>()
        let titleSources = ResizeArray<string * TitleSource>()

        for (root, rootAsGiven, outputRoot) in trees do
            for path in paths do
                if isUnder root path then
                    let name = Path.GetFileName path
                    let folder = Path.GetDirectoryName path
                    let relFolder = Path.GetRelativePath(root, folder)

                    let outputFolderRelativeToRoot =
                        if relFolder = "." then
                            outputRoot
                        else
                            Path.Combine(outputRoot, relFolder)

                    if name.StartsWith '.' then
                        skipped.Add(path, "file name starts with '.'")
                    elif name.StartsWith("_template", StringComparison.Ordinal) then
                        skipped.Add(path, "template")
                    elif isMenuTemplate path then
                        skipped.Add(path, "menu template")
                    else
                        for kind in allKinds do
                            let _, fullOut =
                                Content.getOutputFileNames virtualOutput path kind outputFolderRelativeToRoot

                            fileMap.Add((path, kind), fullOut)

                        if Content.isContentFile path then
                            let otherLang = isOtherLang root folder

                            for kind in allKinds do
                                let template =
                                    match findTemplate pathSet root folder ("_template." + kind.Extension) with
                                    | Some t -> Some t
                                    | None ->
                                        match kind with
                                        | OutputKind.Html -> config.DefaultTemplate
                                        | OutputKind.Markdown when config.GenerateLlmsTxt -> config.DefaultMdTemplate
                                        | _ -> None

                                let hasRoute =
                                    match kind with
                                    | OutputKind.Html -> true
                                    | OutputKind.Markdown when config.GenerateLlmsTxt -> true
                                    | _ -> template.IsSome

                                if hasRoute then
                                    let relOut, _ =
                                        Content.getOutputFileNames virtualOutput path kind outputFolderRelativeToRoot

                                    routes.Add(
                                        urlOf relOut,
                                        ContentPage
                                            {
                                                InputFile = path
                                                OutputKind = kind
                                                Template = template
                                                OutputFileRelativeToRoot = relOut
                                                OutputFolderRelativeToRoot = outputFolderRelativeToRoot
                                                InputFolder = folder
                                                RootInputFolder = rootAsGiven
                                                IsOtherLang = otherLang
                                            }
                                    )

                            if not otherLang then
                                let meta =
                                    match metas.TryFind path with
                                    | Some m -> m
                                    | None -> computeMeta path

                                let relOut, _ =
                                    Content.getOutputFileNames
                                        virtualOutput
                                        path
                                        OutputKind.Html
                                        outputFolderRelativeToRoot

                                navPages.Add
                                    {
                                        InputPath = path
                                        OutputPath = relOut
                                        Title = meta.FrontMatter.Title
                                        Category = meta.FrontMatter.Category
                                        CategoryIndex = meta.FrontMatter.CategoryIndex
                                        Index = meta.FrontMatter.Index
                                    }

                                titleSources.Add(path, meta.FrontMatter.TitleSource)
                        else
                            let relOut, _ =
                                Content.getOutputFileNames virtualOutput path OutputKind.Html outputFolderRelativeToRoot

                            routes.Add(urlOf relOut, StaticFile path)

        routes.Add("/index.json", SearchIndex)

        if config.GenerateLlmsTxt then
            routes.Add("/llms.txt", LlmsTxt)
            routes.Add("/llms-full.txt", LlmsFullTxt)

        {
            Routes = Map.ofSeq routes
            FullPathFileMap = Map.ofSeq fileMap
            FilesWithFrontMatter =
                metas
                |> Map.toSeq
                |> Seq.choose (fun (_, m) -> m.FrontMatterFile)
                |> Content.sortFilesWithFrontMatter
            NavPages = List.ofSeq navPages
            TitleSources = Map.ofSeq titleSources
            Skipped = List.ofSeq skipped
        }

    let emptyApi =
        {
            Phased = None
            Globals = [ ParamKeys.``fsdocs-list-of-namespaces``, "" ]
            CrefResolver = (fun _ -> None)
            Pages = Map.empty
            SearchIndex = [||]
            Error = None
            BuiltAt = None
        }

    let mutable apiBuilding = false

    let buildApi (crack: CrackResult) : ApiState =
        apiBuilding <- true

        try
            try
                match config.GenerateApi crack virtualOutput with
                | None -> emptyApi
                | Some phased ->
                    let model = phased.Model

                    // Used to resolve code references in content with respect to the API Docs model
                    let resolveInlineCodeReference (s: string) =
                        if s.StartsWith("cref:", StringComparison.Ordinal) then
                            match model.Resolver.ResolveCref s.[5..] with
                            | None -> None
                            | Some cref -> Some(cref.NiceName, cref.ReferenceLink)
                        else
                            None

                    {
                        Phased = Some phased
                        Globals = phased.GlobalSubstitutions
                        CrefResolver = resolveInlineCodeReference
                        Pages =
                            phased.Pages
                            |> List.map (fun (file, render) -> file.Replace("\\", "/"), render)
                            |> Map.ofList
                        SearchIndex = phased.SearchIndex
                        Error = None
                        BuiltAt = Some DateTime.Now
                    }
            with ex ->
                printfn "Error : \n%O" ex
                contentOptions.OnError(sprintf "API doc generation failed: %s" ex.Message)

                { emptyApi with
                    Error = Some(string<exn> ex)
                    BuiltAt = Some DateTime.Now
                }
        finally
            apiBuilding <- false

    let stampOf (path: string) : aval<FileStamp option> = AMap.tryFind path files

    let sortedStamps (m: amap<string, FileStamp>) =
        m |> AMap.toAVal |> AVal.map (fun m -> m |> HashMap.toList |> List.sort)

    let crackState = sortedStamps projects |> Adaptive.mapCached (fun _ -> config.Crack())

    /// The site-wide substitutions, from the project files
    let substitutions = crackState |> Adaptive.mapCached (fun c -> c.Substitutions)

    let apiState =
        AVal.map2 (fun stamps crack -> stamps, crack) (sortedStamps dlls) crackState
        |> Adaptive.mapCached (fun (_, crack) -> buildApi crack)

    let allPaths =
        files
        |> AMap.toAVal
        |> AVal.map (fun m -> m |> HashMap.toList |> List.map fst |> List.sort)

    let contentMeta =
        files
        |> AMap.filter (fun p _ -> Content.isContentFile p)
        |> AMap.map (fun p _ -> computeMeta p)

    let metaList =
        contentMeta
        |> AMap.toAVal
        |> AVal.map (fun m -> m |> HashMap.toList |> List.sortBy fst)

    let scan =
        AVal.map2 (fun paths metas -> paths, metas) allPaths metaList
        |> Adaptive.mapCached (fun (paths, metas) -> computeScan paths (Map.ofList metas))

    let navPages = scan |> Adaptive.mapCached (fun s -> s.NavPages)
    let frontMatterList = scan |> Adaptive.mapCached (fun s -> s.FilesWithFrontMatter)
    let fileMap = scan |> Adaptive.mapCached (fun s -> s.FullPathFileMap)

    let htmlRoutes =
        scan
        |> Adaptive.mapCached (fun s ->
            [
                for KeyValue(_, r) in s.Routes do
                    match r with
                    | ContentPage c when c.OutputKind = OutputKind.Html -> yield c
                    | _ -> ()
            ])

    let menuStamps = files |> AMap.filter (fun p _ -> isMenuTemplate p) |> sortedStamps
    let navInputs = AVal.map2 (fun pages menus -> pages, menus) navPages menuStamps

    let templateText (path: string) : aval<string> =
        stampOf path
        |> AVal.map (fun _ -> if File.Exists path then File.ReadAllText path else "")

    let extraText (name: string) =
        Path.Combine(inputRoot, name)
        |> templateText
        |> AVal.map (SimpleTemplating.ApplySubstitutionsInText [ ParamKeys.root, config.Root ])

    let headText = extraText "_head.html"
    let bodyText = extraText "_body.html"

    let pageModels = ConcurrentDictionary<ContentRoute, aval<LiterateDocModel>>()

    let makePageModel (route: ContentRoute) : aval<LiterateDocModel> =
        let path = route.InputFile
        let meta = AMap.tryFind path contentMeta

        let loadStamps =
            meta
            |> AVal.bind (fun m ->
                match m with
                | Some m -> m.Loads |> List.map stampOf |> Adaptive.ofList
                | None -> AVal.constant [])

        let api =
            meta
            |> AVal.bind (fun m ->
                match m with
                | Some m when m.UsesCref -> apiState |> AVal.map Some
                | _ -> AVal.constant None)

        let inputs =
            AVal.custom (fun token ->
                (stampOf path).GetValue token,
                loadStamps.GetValue token,
                api.GetValue token,
                frontMatterList.GetValue token,
                fileMap.GetValue token,
                substitutions.GetValue token)

        inputs
        |> Adaptive.mapCached (fun (_, _, api, filesWithFrontMatter, fileMap, substitutions) ->
            let crefResolver =
                match api with
                | Some a -> a.CrefResolver
                | None -> (fun _ -> None)

            let mdlinkResolver =
                Content.makeMarkdownLinkResolver
                    virtualOutput
                    (route.InputFolder, route.OutputFolderRelativeToRoot, fileMap, route.OutputKind)

            modelComputations <- modelComputations + 1
            computedModels.Enqueue(path, route.OutputKind, DateTime.Now)

            while computedModels.Count > 500 do
                computedModels.TryDequeue() |> ignore

            let model =
                Content.computeModel
                    { contentOptions with
                        Substitutions = substitutions
                    }
                    (Some route.RootInputFolder)
                    path
                    route.OutputKind
                    route.OutputFileRelativeToRoot
                    crefResolver
                    mdlinkResolver
                    filesWithFrontMatter
                    None

            // Evaluating a notebook rewrites it in place; that write must not invalidate this model.
            if config.ContentOptions.Evaluate && Content.isPynbFile path then
                markSelfWrite path

            model)

    let pageModel (route: ContentRoute) =
        pageModels.GetOrAdd(route, makePageModel)

    let apiGlobalKeys = [ "fsdocs-list-of-namespaces"; "fsdocs-body-class" ]

    let templateNeedsApi (template: string option) =
        match template with
        | None -> false
        | Some t ->
            try
                let text = File.ReadAllText t
                apiGlobalKeys |> List.exists (fun k -> text.Contains k)
            with _ ->
                false

    let globalsFor (api: Substitutions option) (navHtml: string) (head: string) (body: string) : Substitutions =
        [
            yield ParamKeys.``fsdocs-watch-script``, config.WatchScript
            yield!
                (match api with
                 | Some g -> g
                 | None -> [ ParamKeys.``fsdocs-list-of-namespaces``, "" ])
            yield ParamKeys.``fsdocs-list-of-documents``, navHtml
            yield ParamKeys.``fsdocs-head-extra``, head
            yield ParamKeys.``fsdocs-body-extra``, body
        ]

    let navHtmlFor (pages: NavPage list) (activePage: string option) =
        Content.getNavigationEntriesFactory config.Root (config.Input, pages, config.IgnoreUncategorized) activePage

    let contentTypeOf (kind: OutputKind) =
        match kind with
        | OutputKind.Html -> "text/html; charset=utf-8"
        | OutputKind.Markdown -> "text/markdown; charset=utf-8"
        | OutputKind.Pynb -> "application/json; charset=utf-8"
        | OutputKind.Fsx
        | OutputKind.Latex -> "text/plain; charset=utf-8"

    let textResponse (contentType: string) (text: string) =
        {
            ContentType = contentType
            Body = Encoding.UTF8.GetBytes text
        }

    let rendered = ConcurrentDictionary<Route, aval<Response option>>()

    let makeContentNode (route: ContentRoute) : aval<Response option> =
        let model = pageModel route

        let templateStamp =
            match route.Template with
            | Some t -> stampOf t
            | None -> AVal.constant None

        let apiGlobals =
            templateStamp
            |> AVal.bind (fun _ ->
                if templateNeedsApi route.Template then
                    apiState |> AVal.map (fun a -> Some a.Globals)
                else
                    AVal.constant None)

        let inputs =
            AVal.custom (fun token ->
                model.GetValue token,
                templateStamp.GetValue token,
                headText.GetValue token,
                bodyText.GetValue token,
                navInputs.GetValue token,
                apiGlobals.GetValue token)

        inputs
        |> Adaptive.mapCached (fun (model, _, head, body, (pages, _), api) ->
            let activePage =
                if route.OutputKind = OutputKind.Html then
                    Some route.InputFile
                else
                    None

            let globals = globalsFor api (navHtmlFor pages activePage) head body
            let text = Content.renderPage model route.Template globals
            Some(textResponse (contentTypeOf route.OutputKind) text))

    let makeApiNode (relativeFile: string) : aval<Response option> =
        let templateStamp =
            match config.ApiDocsTemplate with
            | Some t -> stampOf t
            | None -> AVal.constant None

        let inputs =
            AVal.custom (fun token ->
                apiState.GetValue token,
                templateStamp.GetValue token,
                headText.GetValue token,
                bodyText.GetValue token,
                navInputs.GetValue token)

        inputs
        |> Adaptive.mapCached (fun (api, _, head, body, (pages, _)) ->
            match api.Pages.TryFind relativeFile with
            | None -> None
            | Some render ->
                let globals = globalsFor (Some api.Globals) (navHtmlFor pages None) head body
                let text = render config.ApiDocsTemplate globals
                Some(textResponse (contentTypeOf config.ApiDocsOutputKind) text))

    /// All HTML page models, forced one at a time. Only the search index and llms files need this.
    let allModels =
        AVal.custom (fun token ->
            let routes = htmlRoutes.GetValue token

            [ for r in routes -> r.InputFile, r.IsOtherLang, (pageModel r).GetValue token ])

    let searchIndexEntries =
        AVal.map2
            (fun models (api: ApiState) ->
                Array.append api.SearchIndex (Content.getSearchIndexEntries config.Root models))
            allModels
            apiState

    let searchIndex =
        searchIndexEntries
        |> AVal.map (fun index ->
            Some(textResponse "application/json; charset=utf-8" (System.Text.Json.JsonSerializer.Serialize index)))

    let llms =
        searchIndexEntries
        |> AVal.map (fun index ->
            // When FsDocsGenerateLlmsTxt is enabled, markdown is always generated alongside HTML
            let docContentUsesMarkdown = true
            let apiDocUsesMarkdown = config.ApiDocsOutputKind = OutputKind.Markdown
            LlmsTxt.buildContent config.CollectionName index docContentUsesMarkdown apiDocUsesMarkdown)

    let llmsTxt =
        llms
        |> AVal.map (fun (t, _) -> Some(textResponse "text/plain; charset=utf-8" t))

    let llmsFullTxt =
        llms
        |> AVal.map (fun (_, t) -> Some(textResponse "text/plain; charset=utf-8" t))

    let nodeFor (route: Route) : aval<Response option> option =
        match route with
        | StaticFile _ -> None
        | ContentPage r -> Some(rendered.GetOrAdd(route, fun _ -> makeContentNode r))
        | ApiPage rel -> Some(rendered.GetOrAdd(route, fun _ -> makeApiNode rel))
        | SearchIndex -> Some searchIndex
        | LlmsTxt -> Some llmsTxt
        | LlmsFullTxt -> Some llmsFullTxt

    let resolve (url: string) : Route option =
        let s = AVal.force scan

        match s.Routes.TryFind url with
        | Some r -> Some r
        | None ->
            let apiExtension = "." + config.ApiDocsOutputKind.Extension

            if
                url.StartsWith("/reference/", StringComparison.Ordinal)
                && url.EndsWith(apiExtension, StringComparison.OrdinalIgnoreCase)
            then
                let api = AVal.force apiState
                let rel = url.TrimStart('/')

                if api.Pages.ContainsKey rel then
                    Some(ApiPage rel)
                else
                    None
            else
                None

    let mimeOf (path: string) =
        match Serve.mimeTypesMap (Path.GetExtension(path).ToLowerInvariant()) with
        | Some m -> m.name
        | None -> "application/octet-stream"

    do
        if config.ContentOptions.Evaluate then
            printfn "note, --eval evaluates a script when its page is first requested"

        reconcile ()
        started <- true

    /// The mime type for a static file.
    member _.MimeOf(path: string) = mimeOf path

    /// Resolve a URL path (e.g. '/index.html') to what it is served from.
    member _.Resolve(url: string) : Route option = lock renderLock (fun () -> resolve url)

    /// Compute (or reuse) the response for a URL path. Static files are read from their source.
    member _.Render(url: string) : RenderResult =
        lock renderLock (fun () ->
            match resolve url with
            | None -> NotFound
            | Some(StaticFile path) ->
                if File.Exists path then
                    Rendered
                        {
                            ContentType = mimeOf path
                            Body = File.ReadAllBytes path
                        }
                else
                    NotFound
            | Some route ->
                match nodeFor route with
                | None -> NotFound
                | Some node ->
                    let wasOutOfDate = node.OutOfDate

                    try
                        let response = AVal.force node

                        urlStates.[url] <-
                            {
                                Url = url
                                Valid = true
                                LastBuilt =
                                    (if wasOutOfDate then
                                         Some DateTime.Now
                                     else
                                         urlStates.TryGetValue url
                                         |> function
                                             | true, s -> s.LastBuilt
                                             | _ -> None)
                                LastError = None
                            }

                        match response with
                        | Some r -> Rendered r
                        | None -> NotFound
                    with ex ->
                        printfn "Error : \n%O" ex
                        recordError (sprintf "%s: %s" url ex.Message)

                        urlStates.[url] <-
                            {
                                Url = url
                                Valid = false
                                LastBuilt = None
                                LastError = Some(string<exn> ex)
                            }

                        Failed ex)

    /// Bring the graph up to date with a file that may have changed.
    member _.Refresh(path: string) = refresh FileEvent "changed" path

    /// Walk the watched roots and refresh every difference with the last snapshot.
    member _.Reconcile() = reconcile ()

    /// Raised (with the full path) whenever a change invalidated something in the graph.
    member _.Changed = changedFiles.Publish

    /// The number of content page models computed so far.
    member _.ModelComputations = modelComputations

    /// The content page models computed so far, oldest first (at most the last 500).
    member _.ComputedModels = computedModels |> Seq.toList

    /// The current scan of the input trees.
    member _.Scan = AVal.force scan

    /// The current crack result (re-cracks when a project file changed).
    member _.CrackResult = AVal.force crackState

    /// The API docs state when it has been built, None when it is not built or being built.
    member _.ApiState =
        if apiState.OutOfDate then
            None
        else
            Some(AVal.force apiState)

    /// Whether the API docs are being generated right now.
    member _.ApiBuilding = apiBuilding

    member _.UrlStates = urlStates.Values |> Seq.sortBy (fun s -> s.Url) |> List.ofSeq

    /// The cache state of every URL that has a node, whether requested since its last invalidation or not.
    member _.NodeStates =
        [
            for KeyValue(route, node) in rendered ->
                let url =
                    match route with
                    | ContentPage r -> urlOf r.OutputFileRelativeToRoot
                    | ApiPage rel -> "/" + rel
                    | StaticFile p -> p
                    | SearchIndex -> "/index.json"
                    | LlmsTxt -> "/llms.txt"
                    | LlmsFullTxt -> "/llms-full.txt"

                url, node.OutOfDate
        ]
        |> List.sortBy fst

    member _.Events = events |> Seq.toList

    /// The error messages reported while computing pages and API docs, oldest first.
    member _.Errors = errors |> Seq.toList

    member _.Config = config

    /// Start the file watchers, the reconciler and the background API docs build.
    member this.Start() =
        // File system events are hints to refresh a path now
        let watcherFor (folder: string) (filter: string) =
            let watcher = new FileSystemWatcher(folder, filter, IncludeSubdirectories = true)

            watcher.NotifyFilter <-
                NotifyFilters.LastWrite
                ||| NotifyFilters.FileName
                ||| NotifyFilters.DirectoryName
                ||| NotifyFilters.Size

            watcher.Changed.Add(fun e -> refresh FileEvent "changed" e.FullPath |> ignore)
            watcher.Created.Add(fun e -> refresh FileEvent "created" e.FullPath |> ignore)
            watcher.Deleted.Add(fun e -> refresh FileEvent "deleted" e.FullPath |> ignore)

            watcher.Renamed.Add(fun e ->
                refresh FileEvent "deleted" e.OldFullPath |> ignore
                refresh FileEvent "created" e.FullPath |> ignore)

            watcher.Error.Add(fun _ -> reconcile ())
            watcher.EnableRaisingEvents <- true
            disposables.Add watcher

        for root in treeRoots do
            if Directory.Exists root then
                watcherFor root "*"

        for file in dllPaths @ projectPaths do
            let dir = Path.GetDirectoryName file

            if Directory.Exists dir then
                watcherFor dir (Path.GetFileName file)

        // The reconciler guards against missed events, atomic saves and network mounts
        let reconciler =
            new Timer(
                (fun _ ->
                    (try
                        reconcile ()
                     with ex ->
                         printfn "reconcile failed: %s" ex.Message)),
                null,
                2000,
                2000
            )

        disposables.Add reconciler

        // The API docs are needed by almost every page: build them in the background at startup
        // and again after a DLL or project change, so no request has to wait for them.
        let buildApiInBackground () =
            if not dllPaths.IsEmpty then
                Threading.Tasks.Task.Run(fun () ->
                    lock renderLock (fun () ->
                        try
                            AVal.force apiState |> ignore
                        with ex ->
                            printfn "API docs failed: %s" ex.Message))
                |> ignore

        let apiRebuildScheduled = ref 0

        this.Changed.Add(fun path ->
            if dllSet.Contains path || projectSet.Contains path then
                if Interlocked.Exchange(&apiRebuildScheduled.contents, 1) = 0 then
                    async {
                        do! Async.Sleep 500
                        Interlocked.Exchange(&apiRebuildScheduled.contents, 0) |> ignore
                        buildApiInBackground ()
                    }
                    |> Async.Start)

        buildApiInBackground ()

    interface IDisposable with
        member _.Dispose() =
            for d in disposables do
                d.Dispose()

/// The state of the watch session, as shown by /.fsdocs/doctor and /.fsdocs/doctor.json.
/// Plain records and strings only, so System.Text.Json can serialize it.
type DoctorSubstitution =
    {
        Key: string
        Value: string
        Source: string
    }

type DoctorProject =
    {
        ProjectFile: string
        TargetPath: string
        TargetExists: bool
        /// 'resolved' once the design-time build ran, else 'not resolved yet'
        ReferencesStatus: string
        References: string list
        DroppedReferences: string list
        OverridingSubstitutions: DoctorSubstitution list
    }

type DoctorRoute =
    {
        Url: string
        Kind: string
        Source: string
        Template: string option
    }

type DoctorNavPage =
    {
        Title: string
        TitleSource: string
        Category: string option
        CategoryIndex: int option
        Index: int option
        Source: string
        Url: string
    }

type DoctorSkipped = { Path: string; Reason: string }

type DoctorApi =
    {
        Dlls: string list
        Template: string option
        OutputKind: string
        Status: string
        BuiltAt: DateTime option
        Error: string option
        Namespaces: int
        Entities: int
        Pages: int
    }

type DoctorUrl =
    {
        Url: string
        State: string
        LastBuilt: DateTime option
        LastError: string option
    }

type DoctorEvent =
    {
        Time: DateTime
        Path: string
        Change: string
        Invalidated: bool
    }

type DoctorComputed =
    {
        Time: DateTime
        Source: string
        OutputKind: string
    }

type DoctorError = { Time: DateTime; Message: string }

type Doctor =
    {
        ToolVersion: string
        CommandLine: string
        Command: string
        Input: string
        Root: string
        CollectionName: string
        GenerateLlmsTxt: bool
        IgnoredOptions: IgnoredOption list
        Projects: DoctorProject list
        Substitutions: DoctorSubstitution list
        DefaultTemplate: ResolutionDiagnostics
        DefaultMarkdownTemplate: ResolutionDiagnostics
        ApiDocsTemplate: ResolutionDiagnostics
        Extras: ResolutionDiagnostics
        HeadTemplate: string option
        BodyTemplate: string option
        MenuTemplatesFound: bool
        Routes: DoctorRoute list
        NavPages: DoctorNavPage list
        Skipped: DoctorSkipped list
        Api: DoctorApi
        Urls: DoctorUrl list
        Events: DoctorEvent list
        ComputedModels: DoctorComputed list
        Errors: DoctorError list
    }

module internal Doctor =

    let ofSite (site: Site) : Doctor =
        let d = site.Config.Diagnostics
        let scan = site.Scan
        let api = site.ApiState
        let crack = site.CrackResult

        let referencesByProject = crack.References |> List.map (fun r -> r.ProjectFile, r) |> Map.ofList

        let substitution (s: SubstitutionDiagnostics) =
            {
                Key = s.Key
                Value = s.Value
                Source =
                    match s.Source with
                    | Project -> "project"
                    | Parameters -> "--parameters"
                    | WatchOverride -> "watch override"
            }

        let titleSource (t: TitleSource) =
            match t with
            | TitleSource.FrontMatter -> "front matter"
            | TitleSource.Heading -> "heading"
            | TitleSource.FileName -> "file name"

        let states = site.UrlStates |> List.map (fun s -> s.Url, s) |> Map.ofList

        {
            ToolVersion = d.ToolVersion
            CommandLine = d.CommandLine
            Command = d.Command
            Input = d.Input
            Root = d.Root
            CollectionName = d.CollectionName
            GenerateLlmsTxt = d.GenerateLlmsTxt
            IgnoredOptions = d.IgnoredOptions
            Projects =
                [
                    for p in crack.Projects ->
                        let references = referencesByProject.TryFind p.ProjectFile

                        {
                            ProjectFile = p.ProjectFile
                            TargetPath = p.TargetPath
                            TargetExists = p.TargetExists
                            ReferencesStatus =
                                (match references with
                                 | Some _ -> "resolved"
                                 | None -> "not resolved")
                            References = references |> Option.map (fun r -> r.References) |> Option.defaultValue []
                            DroppedReferences =
                                references
                                |> Option.map (fun r -> r.DroppedReferences)
                                |> Option.defaultValue []
                            OverridingSubstitutions =
                                [
                                    for (k, v) in p.OverridingSubstitutions ->
                                        {
                                            Key = k
                                            Value = v
                                            Source = "project"
                                        }
                                ]
                        }
                ]
            Substitutions = crack.SubstitutionDiagnostics |> List.map substitution
            DefaultTemplate = d.DefaultTemplate
            DefaultMarkdownTemplate = d.DefaultMarkdownTemplate
            ApiDocsTemplate = d.ApiDocsTemplate
            Extras = d.Extras
            HeadTemplate = d.HeadTemplate
            BodyTemplate = d.BodyTemplate
            MenuTemplatesFound = d.MenuTemplatesFound
            Routes =
                [
                    for KeyValue(url, route) in scan.Routes ->
                        match route with
                        | ContentPage r ->
                            {
                                Url = url
                                Kind = sprintf "content (%s)" r.OutputKind.Extension
                                Source = r.InputFile
                                Template = r.Template
                            }
                        | StaticFile path ->
                            {
                                Url = url
                                Kind = "static file"
                                Source = path
                                Template = None
                            }
                        | ApiPage rel ->
                            {
                                Url = url
                                Kind = "API docs"
                                Source = rel
                                Template = site.Config.ApiDocsTemplate
                            }
                        | SearchIndex ->
                            {
                                Url = url
                                Kind = "search index"
                                Source = "all pages and the API docs"
                                Template = None
                            }
                        | LlmsTxt
                        | LlmsFullTxt ->
                            {
                                Url = url
                                Kind = "llms.txt"
                                Source = "all pages and the API docs"
                                Template = None
                            }
                ]
            NavPages =
                [
                    for p in scan.NavPages ->
                        {
                            Title = p.Title.Trim()
                            TitleSource =
                                scan.TitleSources.TryFind p.InputPath
                                |> Option.map titleSource
                                |> Option.defaultValue ""
                            Category = p.Category
                            CategoryIndex = p.CategoryIndex
                            Index = p.Index
                            Source = p.InputPath
                            Url = p.Uri site.Config.Root
                        }
                ]
            Skipped = [ for (path, reason) in scan.Skipped -> { Path = path; Reason = reason } ]
            Api =
                {
                    Dlls = site.Config.ApiDllPaths
                    Template = site.Config.ApiDocsTemplate
                    OutputKind = string<OutputKind> site.Config.ApiDocsOutputKind
                    Status =
                        match api with
                        | None when site.ApiBuilding -> "building"
                        | None -> "not built"
                        | Some a when a.Error.IsSome -> "failed"
                        | Some a when a.Phased.IsNone -> "nothing to generate"
                        | Some _ -> "built"
                    BuiltAt = api |> Option.bind (fun a -> a.BuiltAt)
                    Error = api |> Option.bind (fun a -> a.Error)
                    Namespaces =
                        api
                        |> Option.bind (fun a -> a.Phased)
                        |> Option.map (fun p -> p.Model.Collection.Namespaces.Length)
                        |> Option.defaultValue 0
                    Entities =
                        api
                        |> Option.bind (fun a -> a.Phased)
                        |> Option.map (fun p ->
                            p.Model.Collection.Namespaces |> List.sumBy (fun ns -> ns.Entities.Length))
                        |> Option.defaultValue 0
                    Pages = api |> Option.map (fun a -> a.Pages.Count) |> Option.defaultValue 0
                }
            Urls =
                [
                    for (url, outOfDate) in site.NodeStates ->
                        let state = states.TryFind url

                        {
                            Url = url
                            State =
                                match state with
                                | Some s when s.LastError.IsSome -> "error"
                                | _ when outOfDate -> "out of date"
                                | _ -> "valid"
                            LastBuilt = state |> Option.bind (fun s -> s.LastBuilt)
                            LastError = state |> Option.bind (fun s -> s.LastError)
                        }
                ]
            Events =
                [
                    for e in site.Events ->
                        {
                            Time = e.Time
                            Path = e.Path
                            Change = e.Change
                            Invalidated = e.Invalidated
                        }
                ]
            ComputedModels =
                [
                    for (path, kind, time) in site.ComputedModels ->
                        {
                            Time = time
                            Source = path
                            OutputKind = string<OutputKind> kind
                        }
                ]
            Errors = [ for (time, msg) in site.Errors -> { Time = time; Message = msg } ]
        }

    let toJson (doctor: Doctor) =
        System.Text.Json.JsonSerializer.Serialize(doctor, System.Text.Json.JsonSerializerOptions(WriteIndented = true))

    /// A single HTML page with inline styles, independent of the site template so it works when that is broken.
    let toHtml (doctor: Doctor) =
        let sb = StringBuilder()
        let encode (s: string) = System.Web.HttpUtility.HtmlEncode s
        let str (o: string option) = defaultArg o ""

        let time (t: DateTime option) =
            t |> Option.map (fun t -> t.ToString("HH:mm:ss")) |> str

        let section (title: string) =
            sb.AppendFormat("<h2>{0}</h2>\n", encode title) |> ignore

        let para (text: string) =
            sb.AppendFormat("<p>{0}</p>\n", encode text) |> ignore

        let table (headers: string list) (rows: string list list) =
            if rows.IsEmpty then
                para "none"
            else
                sb.Append("<table><thead><tr>") |> ignore

                for h in headers do
                    sb.AppendFormat("<th>{0}</th>", encode h) |> ignore

                sb.Append("</tr></thead><tbody>\n") |> ignore

                for row in rows do
                    sb.Append("<tr>") |> ignore

                    for cell in row do
                        sb.AppendFormat("<td>{0}</td>", encode cell) |> ignore

                    sb.Append("</tr>\n") |> ignore

                sb.Append("</tbody></table>\n") |> ignore

        let resolution (name: string) (r: ResolutionDiagnostics) =
            [ name; str r.Chosen; String.concat ", " r.Tried; str r.Note ]

        sb.Append(
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>fsdocs doctor</title><style>"
            + "body{font-family:system-ui,sans-serif;margin:2rem;color:#222}table{border-collapse:collapse;margin:.5rem 0 1rem;font-size:.9rem}"
            + "th,td{border:1px solid #ccc;padding:.2rem .5rem;text-align:left;vertical-align:top;word-break:break-all}th{background:#eee}"
            + "h2{margin-top:2rem;border-bottom:1px solid #ccc}code{background:#f4f4f4;padding:0 .2rem}.err{color:#b00}"
            + "</style></head><body>\n"
        )
        |> ignore

        sb.AppendFormat(
            "<h1>fsdocs doctor</h1>\n<p>fsdocs {0}, <code>fsdocs {1}</code>. ",
            encode doctor.ToolVersion,
            encode doctor.CommandLine
        )
        |> ignore

        sb.Append("Machine readable: <a href=\"/.fsdocs/doctor.json\">/.fsdocs/doctor.json</a></p>\n")
        |> ignore

        section "Session"

        table
            [ "Setting"; "Value" ]
            [
                [ "command"; doctor.Command ]
                [ "input"; doctor.Input ]
                [ "root"; doctor.Root ]
                [ "collection name"; doctor.CollectionName ]
                [ "llms.txt"; string<bool> doctor.GenerateLlmsTxt ]
                [ "_head.html"; str doctor.HeadTemplate ]
                [ "_body.html"; str doctor.BodyTemplate ]
                [ "menu templates"; string<bool> doctor.MenuTemplatesFound ]
            ]

        if not doctor.IgnoredOptions.IsEmpty then
            table [ "Ignored option"; "Reason" ] [ for o in doctor.IgnoredOptions -> [ "--" + o.Option; o.Reason ] ]

        section "Errors"
        table [ "Time"; "Message" ] [ for e in doctor.Errors -> [ e.Time.ToString("HH:mm:ss"); e.Message ] ]

        section "Projects"

        table
            [ "Project"; "Target"; "Exists"; "References"; "References dropped"; "Overriding substitutions" ]
            [
                for p in doctor.Projects ->
                    [
                        p.ProjectFile
                        p.TargetPath
                        string<bool> p.TargetExists
                        (if p.ReferencesStatus = "resolved" then
                             sprintf "%d resolved" p.References.Length
                         else
                             p.ReferencesStatus)
                        String.concat ", " p.DroppedReferences
                        p.OverridingSubstitutions
                        |> List.map (fun s -> sprintf "%s = %s" s.Key s.Value)
                        |> String.concat ", "
                    ]
            ]

        section "API docs"

        table
            [ "Setting"; "Value" ]
            [
                [ "status"; doctor.Api.Status ]
                [ "built at"; time doctor.Api.BuiltAt ]
                [ "output kind"; doctor.Api.OutputKind ]
                [ "template"; str doctor.Api.Template ]
                [ "dlls"; String.concat ", " doctor.Api.Dlls ]
                [ "namespaces"; string<int> doctor.Api.Namespaces ]
                [ "entities"; string<int> doctor.Api.Entities ]
                [ "pages"; string<int> doctor.Api.Pages ]
                [ "error"; str doctor.Api.Error ]
            ]

        section "Substitutions"
        table [ "Key"; "Value"; "Source" ] [ for s in doctor.Substitutions -> [ s.Key; s.Value; s.Source ] ]

        section "Templates and extras"

        table
            [ "What"; "Chosen"; "Tried"; "Note" ]
            [
                resolution "default template" doctor.DefaultTemplate
                resolution "default markdown template" doctor.DefaultMarkdownTemplate
                resolution "API docs template" doctor.ApiDocsTemplate
                resolution "extras folder" doctor.Extras
            ]

        section "Navigation"

        table
            [ "Title"; "Title from"; "Category"; "Category index"; "Index"; "Source"; "Url" ]
            [
                for p in doctor.NavPages ->
                    [
                        p.Title
                        p.TitleSource
                        str p.Category
                        p.CategoryIndex |> Option.map string<int> |> str
                        p.Index |> Option.map string<int> |> str
                        p.Source
                        p.Url
                    ]
            ]

        section "Requested pages"

        table
            [ "Url"; "State"; "Last built"; "Error" ]
            [ for u in doctor.Urls -> [ u.Url; u.State; time u.LastBuilt; str u.LastError ] ]

        section "Computed page models"

        table
            [ "Time"; "Source"; "Output" ]
            [ for c in List.rev doctor.ComputedModels -> [ c.Time.ToString("HH:mm:ss"); c.Source; c.OutputKind ] ]

        section "Recent file events"

        table
            [ "Time"; "Path"; "Change"; "Invalidated" ]
            [
                for e in List.rev doctor.Events ->
                    [ e.Time.ToString("HH:mm:ss"); e.Path; e.Change; string<bool> e.Invalidated ]
            ]

        section "Routes"

        table
            [ "Url"; "Kind"; "Source"; "Template" ]
            [ for r in doctor.Routes -> [ r.Url; r.Kind; r.Source; str r.Template ] ]

        section "Skipped files"
        table [ "Path"; "Reason" ] [ for s in doctor.Skipped -> [ s.Path; s.Reason ] ]

        sb.Append("</body></html>\n") |> ignore
        sb.ToString()

/// The Suave application serving a Site.
module internal DevServer =

    let errorPage (url: string) (ex: exn) =
        let encode (s: string) = System.Web.HttpUtility.HtmlEncode s

        sprintf
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>fsdocs: error</title></head><body style=\"font-family: sans-serif\"><h1>fsdocs could not build %s</h1><pre style=\"white-space: pre-wrap\">%s</pre><p>Fix the file and reload. See <a href=\"/.fsdocs/doctor\">/.fsdocs/doctor</a>.</p>%s</body></html>"
            (encode url)
            (encode (string<exn> ex))
            (Serve.generateWatchScript ())

    let app (site: Site) (liveReload: LiveReload) : WebPart =
        let noCache =
            Writers.setHeader "Cache-Control" "no-cache, no-store, must-revalidate"
            >=> Writers.setHeader "Pragma" "no-cache"
            >=> Writers.setHeader "Expires" "0"

        let serve (ctx: HttpContext) : Async<HttpContext option> =
            async {
                // Long computations must not block Suave's accept loop
                do! Async.SwitchToThreadPool()
                let url = Uri.UnescapeDataString ctx.request.path

                match site.Resolve url with
                | Some(StaticFile path) when File.Exists path ->
                    return! (Writers.setMimeType (site.MimeOf path) >=> Files.sendFile path false) ctx
                | _ ->
                    match site.Render url with
                    | Rendered r -> return! (Writers.setMimeType r.ContentType >=> Successful.ok r.Body) ctx
                    | NotFound -> return! RequestErrors.NOT_FOUND (sprintf "fsdocs: nothing is served at %s" url) ctx
                    | Failed ex ->
                        return!
                            (Writers.setMimeType "text/html; charset=utf-8"
                             >=> ServerErrors.INTERNAL_ERROR(errorPage url ex))
                                ctx
            }

        let doctor (render: Doctor -> string) (mime: string) (ctx: HttpContext) =
            async {
                do! Async.SwitchToThreadPool()

                try
                    let text = render (Doctor.ofSite site)
                    return! (Writers.setMimeType mime >=> Successful.OK text) ctx
                with ex ->
                    return!
                        (Writers.setMimeType "text/plain; charset=utf-8"
                         >=> ServerErrors.INTERNAL_ERROR(string<exn> ex))
                            ctx
            }

        choose
            [
                path "/" >=> Redirection.redirect "/index.html"
                path "/websocket" >=> handShake liveReload.SocketHandler
                path "/.fsdocs/doctor"
                >=> noCache
                >=> doctor Doctor.toHtml "text/html; charset=utf-8"
                path "/.fsdocs/doctor.json"
                >=> noCache
                >=> doctor Doctor.toJson "application/json; charset=utf-8"
                noCache >=> serve
            ]

    /// Broadcast the site's changes to the browsers, 300 ms after the last change:
    /// css files are hot swapped, anything else reloads the page.
    let connectLiveReload (site: Site) (liveReload: LiveReload) =
        let pending = ConcurrentQueue<string>()
        let flushScheduled = ref 0

        let drain () =
            let names = System.Collections.Generic.HashSet<string>()
            let mutable more = true

            while more do
                match pending.TryDequeue() with
                | true, path -> names.Add(Path.GetFileName path) |> ignore
                | _ -> more <- false

            names |> Seq.toList

        let flush () =
            try
                let css, others =
                    drain ()
                    |> List.partition (fun n -> n.EndsWith(".css", StringComparison.OrdinalIgnoreCase))

                if not others.IsEmpty then
                    printfn "Detected change in %s, browser will reload" (String.concat ", " others)
                    liveReload.Broadcast "full"
                else
                    for c in css do
                        printfn "Detected change in %s, hot swapping css" c
                        liveReload.Broadcast c
            with ex ->
                printfn "browser reload failed: %O" ex

        site.Changed.Add(fun path ->
            pending.Enqueue path

            if Interlocked.Exchange(&flushScheduled.contents, 1) = 0 then
                async {
                    do! Async.Sleep 300
                    Interlocked.Exchange(&flushScheduled.contents, 0) |> ignore
                    flush ()
                }
                |> Async.Start)

    let startWebServer (site: Site) (port: int) =
        let liveReload = LiveReload()
        connectLiveReload site liveReload
        Serve.startWebServer (app site liveReload) port
