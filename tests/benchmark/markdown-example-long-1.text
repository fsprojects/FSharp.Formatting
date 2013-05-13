This is a rather long question, so please bare with me.

We are implementing an emulator for a piece of hardware that
is being developed at the same time. The idea is to give 3rd parties
a software solution to test their client software and give the hardware
developers a reference point to implement their firmware.

The people who wrote the protocol for the hardware used a custom
version of SUN XDR called INCA_XDR. It's a tool to serialize and
de-serialize messages. It's written in C and we want to avoid any
native code so we are parsing the protocol data manually.

The protocol is by nature rather complex and the data packets
can have many different structures, but it always has the same global structure:

> [HEAD] [INTRO] [DATA] [TAIL]

    [HEAD] =
        byte sync 0x03
        byte length X       [MSB]       X = length of [HEADER] + [INTRO] + [DATA]
        byte length X       [LSB]       X = length of [HEADER] + [INTRO] + [DATA]
        byte check X        [MSB]       X = crc of [INTRO] [DATA]
        byte check X        [LSB]       X = crc of [INTRO] [DATA]
        byte headercheck X              X = XOR over [SYNC] [LENGTH] [CHECK]

    [INTRO]
        byte version 0x03
        byte address X                  X = 0 for point-to-point, 1-254 for specific controller, 255 = broadcast
        byte sequence X                 X = sequence number
        byte group X        [MSB]       X = The category of the message
        byte group X        [LSB]       X = The category of the message
        byte type X         [MSB]       X = The id of the message
        byte type X         [LSB]       X = The id of the message

    [DATA] =
        The actuall data for the specified message,
        this format really differs a lot.
        
        It always starts with a DRCode which is one byte.
        It more or less specifies the general structure of
        the data, but even within the same structure the data
        can mean many different things and have different lenghts.
        (I think this is an artifact of the INCA_XDR tool)

    [TAIL] =
        byte 0x0D

As you can see there is a lot of overhead data, but this is because
the protocol needs to work with both RS232 (point-to-multipoint) and TCP/IP (p2p).

        name        size    value
        drcode	    1	    1	
        name	    8		        contains a name that can be used as a file name (only alphanumeric characters allowed)
        timestamp	14	            yyyymmddhhmmss	contains timestamp of bitmap library
        size	    4		        size of bitmap library to be loaded
        options	    1		        currently no options

Or it might have an entirely different structure:

        name        size    value
        drcode	    1	    2	
        lastblock	1	    0 - 1	1 indicates last block. Firmware can be stored
        blocknumber	2		        Indicates block of firmware
        blocksize	2	    N	    size of block to load
        blockdata	N		        data of block of firmware

Sometimes it's just a DRCode and no additional data.

Based on the group and the type field, the emulator
needs to perform certain actions. So first we look at those
two fields and based on that we know what to expect of the data
and have to parse it properly.

Then the response data needs to be generated which again has
many different data structures. Some messages simply generate
an ACK or NACK message, while others generate a real reply with data.

We decided to break things up in small pieces.

First of all there is the IDataProcessor.

Classes implementing this interface are responsible
for validating raw data and generating instances of the Message class.
They are not responsible for commmunication, they are simply passed a byte[]

Raw data validation means checking the header for checksum, crc and length errors.

The resulting message gets passed to a class that implements IMessageProcessor.
Even if the raw data was considered invalid, because the IDataProcessor has no
notion of response messages or anything else, all it does is validate the raw data.

To inform the IMessageProcessor about errors, some additional properties have been added
to the Message class:

    bool nakError = false;
    bool tailError = false;
    bool crcError = false;
    bool headerError = false;
    bool lengthError = false;

They are not related to the protocol and only exist for the IMessageProcessor

The IMessageProcessor is where the real work is done.
Because of all the different message groups and types I decided to
use F# to implement the IMessageProcessor interface because pattern matching
seemed like a good way to avoid lots of nested if/else and caste statements.
(I have no prior experience with F# or even functional languages other than LINQ and SQL)

The IMessageProcessor analyzes the data and decides what methods it should call
on the IHardwareController. It might seem redundant to have IHardwareController,
but we want to be able to swap it out with a different implementation
and not be forced to use F# either. The current implementation is a WPF windows,
but it might be a Cocoa# window or simply a console for example.

The IHardwareController is also responsible for managing state because
the developers should be able to manipulate hardware parameters and errors through the user interface.

So once the IMessageProcessor has called the correct methods on IHardwareController,
it has to generate the response MEssage. Again... the data in these response messages
can have many different structures.

Eventually an IDataFactory is used to convert the Message to raw protocol data
ready to be sent to whatever class is responsible for communication.
(Additional encapsulation of the data might be required for example)

This is nothing "hard" about writing this code, but all the different
commands and data structures require lots and lots of code and there are few
things we can reuse. (At least as far as I can see now, hoping someone can prove me wrong)

This is the first time I use F#, so I'm actually learning as I go. The code below is far from finished
and probably looks like a giant mess. It only implements a handfull of all the messages in the protocol
and I can tell you there are lots and lots of them. So this file is going to get huge!

Important to know: the byte order is reversed over the wire (historical reasons)


    module Arendee.Hardware.MessageProcessors
    
    open System;
    open System.Collections
    open Arendee.Hardware.Extenders
    open Arendee.Hardware.Interfaces
    open System.ComponentModel.Composition
    open System.Threading
    open System.Text
    
    let VPL_NOERROR = (uint16)0
    let VPL_CHECKSUM = (uint16)1
    let VPL_FRAMELENGTH = (uint16)2
    let VPL_OUTOFSEQUENCE = (uint16)3
    let VPL_GROUPNOTSUPPORTED = (uint16)4
    let VPL_REQUESTNOTSUPPORTED = (uint16)5
    let VPL_EXISTS = (uint16)6
    let VPL_INVALID = (uint16)7
    let VPL_TYPERROR = (uint16)8
    let VPL_NOTLOADING = (uint16)9
    let VPL_NOTFOUND = (uint16)10
    let VPL_OUTOFMEM = (uint16)11
    let VPL_INUSE = (uint16)12
    let VPL_SIZE = (uint16)13
    let VPL_BUSY = (uint16)14
    let SYNC_BYTE = (byte)0xE3
    let TAIL_BYTE = (byte)0x0D
    let MESSAGE_GROUP_VERSION = 3uy
    let MESSAGE_GROUP = 701us
    
      
    [<Export(typeof<IMessageProcessor>)>]
    type public StandardMessageProcessor() = class
        let mutable controller : IHardwareController = null               
        
        interface IMessageProcessor with
            member this.ProcessMessage m : Message = 
                printfn "%A" controller.Status
                controller.Status <- ControllerStatusExtender.DisableBit(controller.Status,ControllerStatus.Nak)
    
                match m with
                | m when m.LengthError -> this.nakResponse(m,VPL_FRAMELENGTH)
                | m when m.CrcError -> this.nakResponse(m,VPL_CHECKSUM)
                | m when m.HeaderError -> this.nakResponse(m,VPL_CHECKSUM)
                | m -> this.processValidMessage m
                | _ -> null      
            
            member public x.HardwareController
                with get () = controller
                and set y = controller <- y                 
        end
        
        member private this.processValidMessage (m : Message) =
            match m.Intro.MessageGroup with
            | 701us -> this.processDefaultGroupMessage(m);
            | _ -> this.nakResponse(m, VPL_GROUPNOTSUPPORTED);
        
        member private this.processDefaultGroupMessage(m : Message) =
            match m.Intro.MessageType with
            | (1us) -> this.firmwareVersionListResponse(m)                        //ListFirmwareVersions              0
            | (2us) -> this.StartLoadingFirmwareVersion(m)                     //StartLoadingFirmwareVersion       1
            | (3us) -> this.LoadFirmwareVersionBlock(m)                     //LoadFirmwareVersionBlock          2
            | (4us) -> this.nakResponse(m, VPL_FRAMELENGTH)                       //RemoveFirmwareVersion             3
            | (5us) -> this.nakResponse(m, VPL_FRAMELENGTH)                       //ActivateFirmwareVersion           3        
            | (12us) -> this.nakResponse(m,VPL_FRAMELENGTH)                       //StartLoadingBitmapLibrary         2
            | (13us) -> this.nakResponse(m,VPL_FRAMELENGTH)                       //LoadBitmapLibraryBlock            2        
            | (21us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //ListFonts                         0
            | (22us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //LoadFont                          4
            | (23us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //RemoveFont                        3
            | (24us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //SetDefaultFont                    3         
            | (31us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //ListParameterSets                 0
            | (32us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //LoadParameterSets                 4
            | (33us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //RemoveParameterSet                3
            | (34us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //ActivateParameterSet              3
            | (35us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //GetParameterSet                   3        
            | (41us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //StartSelfTest                     0
            | (42us) -> this.returnStatus(m)                                      //GetStatus                         0
            | (43us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //GetStatusDetail                   0
            | (44us) -> this.ResetStatus(m)                     //ResetStatus                       5
            | (45us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //SetDateTime                       6
            | (46us) -> this.nakResponse(m, VPL_FRAMELENGTH)                      //GetDateTime                       0
            | _ -> this.nakResponse(m, VPL_REQUESTNOTSUPPORTED)
        
    
    
        (* The various responses follow *)
    
        //Generate a NAK response
        member private this.nakResponse (message : Message , error) =
            controller.Status <- controller.Status ||| ControllerStatus.Nak
            let intro = new MessageIntro()
            intro.MessageGroupVersion <- MESSAGE_GROUP_VERSION
            intro.Address <- message.Intro.Address
            intro.SequenceNumber <- this.setHigh(message.Intro.SequenceNumber)
            intro.MessageGroup <- MESSAGE_GROUP
            intro.MessageType <- 130us
            let errorBytes = UShortExtender.ToIntelOrderedByteArray(error)
            let data = Array.zero_create(5)
            let x = this.getStatusBytes
            let y = this.getStatusBytes
            data.[0] <- 7uy
            data.[1..2] <- this.getStatusBytes
            data.[3..4] <- errorBytes      
            let header = this.buildHeader intro data
            let message = new Message()
            message.Header <- header
            message.Intro <- intro
            message.Tail <- TAIL_BYTE
            message.Data <- data
            message   
            
        //Generate an ACK response
        member private this.ackResponse (message : Message) =   
            let intro = new MessageIntro()
            intro.MessageGroupVersion <- MESSAGE_GROUP_VERSION
            intro.Address <- message.Intro.Address
            intro.SequenceNumber <- this.setHigh(message.Intro.SequenceNumber)
            intro.MessageGroup <- MESSAGE_GROUP
            intro.MessageType <- 129us
            let data = Array.zero_create(3);
            data.[0] <- 0x05uy
            data.[1..2] <- this.getStatusBytes
            let header = this.buildHeader intro data
            message.Header <- header
            message.Intro <- intro
            message.Tail <- TAIL_BYTE
            message.Data <- data
            message        
            
        //Generate a ReturnFirmwareVersionList
        member private this.firmwareVersionListResponse (message : Message) =
            //Validation
            if message.Data.[0] <> 0x00uy then
               this.nakResponse(message,VPL_INVALID)
            else
                let intro = new MessageIntro()
                intro.MessageGroupVersion <- MESSAGE_GROUP_VERSION
                intro.Address <- message.Intro.Address
                intro.SequenceNumber <- this.setHigh(message.Intro.SequenceNumber)
                intro.MessageGroup <- MESSAGE_GROUP
                intro.MessageType <- 132us    
                let firmwareVersions = controller.ReturnFirmwareVersionList();
                let firmwareVersionBytes = BitConverter.GetBytes((uint16)firmwareVersions.Count) |> Array.rev
                
                //Create the data
                let data = Array.zero_create(3 + (int)firmwareVersions.Count * 27)
                data.[0] <- 0x09uy                              //drcode
                data.[1..2] <- firmwareVersionBytes             //Number of firmware versions
                
                let mutable index = 0
                let loops = firmwareVersions.Count - 1
                for i = 0 to loops do
                    let nameBytes = ASCIIEncoding.ASCII.GetBytes(firmwareVersions.[i].Name) |>  Array.rev
                    let timestampBytes = this.getTimeStampBytes firmwareVersions.[i].Timestamp |> Array.rev
                    let sizeBytes = BitConverter.GetBytes(firmwareVersions.[i].Size) |> Array.rev
                                   
                    data.[index + 3 .. index + 10] <- nameBytes
                    data.[index + 11 .. index + 24] <- timestampBytes
                    data.[index + 25 .. index + 28] <- sizeBytes
                    data.[index + 29] <- firmwareVersions.[i].Status
                    index <- index + 27            
               
                let header = this.buildHeader intro data
                message.Header <- header
                message.Intro <- intro
                message.Data <- data
                message.Tail <- TAIL_BYTE
                message
                
        //Generate ReturnStatus
        member private this.returnStatus (message : Message) =
            //Validation
            if message.Data.[0] <> 0x00uy then
               this.nakResponse(message,VPL_INVALID)
            else
                let intro = new MessageIntro()
                intro.MessageGroupVersion <- MESSAGE_GROUP_VERSION
                intro.Address <- message.Intro.Address
                intro.SequenceNumber <- this.setHigh(message.Intro.SequenceNumber)
                intro.MessageGroup <- MESSAGE_GROUP
                intro.MessageType <- 131us
                
                let statusDetails = controller.ReturnStatus();
                
                let sizeBytes = BitConverter.GetBytes((uint16)statusDetails.Length) |> Array.rev
       
                let detailBytes = ASCIIEncoding.ASCII.GetBytes(statusDetails) |> Array.rev
                                     
                let data = Array.zero_create(statusDetails.Length + 5)
                data.[0] <- 0x08uy
                data.[1..2] <- this.getStatusBytes
                data.[3..4] <- sizeBytes    //Details size
                data.[5..5 + statusDetails.Length - 1] <- detailBytes
                
                let header = this.buildHeader intro data
                message.Header <- header
                message.Intro <- intro
                message.Data <- data
                message.Tail <- TAIL_BYTE
                message
        
        //Reset some status bytes    
        member private this.ResetStatus (message : Message) =
            if message.Data.[0] <> 0x05uy then
                this.nakResponse(message, VPL_INVALID)
            else        
                let flagBytes = message.Data.[1..2] |> Array.rev 
                let flags = Enum.ToObject(typeof<ControllerStatus>,BitConverter.ToInt16(flagBytes,0)) :?> ControllerStatus
                let retVal = controller.ResetStatus flags
                
                if retVal <> 0x00us then
                    this.nakResponse(message,retVal)
                else
                    this.ackResponse(message)
                
        //StartLoadingFirmwareVersion (Ack/Nak)
        member private this.StartLoadingFirmwareVersion (message : Message) =
            if (message.Data.[0] <> 0x01uy) then
                this.nakResponse(message, VPL_INVALID)
            else
                //Analyze the data
                let name = message.Data.[1..8] |> Array.rev |> ASCIIEncoding.ASCII.GetString
                let text = message.Data.[9..22] |> Array.rev |> Seq.map(fun x -> ASCIIEncoding.ASCII.GetBytes(x.ToString()).[0]) |> Seq.to_array |> ASCIIEncoding.ASCII.GetString
                let timestamp = DateTime.ParseExact(text,"yyyyMMddHHmmss",Thread.CurrentThread.CurrentCulture)
                
                let size = BitConverter.ToUInt32(message.Data.[23..26] |> Array.rev,0)
                let overwrite = 
                    match message.Data.[27] with
                    | 0x00uy -> false
                    | _ -> true
                            
                //Create a FirmwareVersion instance
                let firmware = new FirmwareVersion();
                firmware.Name <- name
                firmware.Timestamp <- timestamp
                firmware.Size <- size
                
                let retVal = controller.StartLoadingFirmwareVersion(firmware,overwrite)
                
                if retVal <> 0x00us then
                    this.nakResponse(message, retVal) //The controller denied the request
                else
                    this.ackResponse(message);
                    
        //LoadFirmwareVersionBlock (ACK/NAK)
        member private this.LoadFirmwareVersionBlock (message : Message) =
            if message.Data.[0] <> 0x02uy then
                this.nakResponse(message, VPL_INVALID)
            else
                //Analyze the data
                let lastBlock = 
                    match message.Data.[1] with
                    | 0x00uy -> false
                    | _true -> true
                
                let blockNumber = BitConverter.ToUInt16(message.Data.[2..3] |> Array.rev,0)            
                let blockSize = BitConverter.ToUInt16(message.Data.[4..5] |> Array.rev,0)
                let blockData = message.Data.[6..6 + (int)blockSize - 1] |> Array.rev
                
                let retVal = controller.LoadFirmwareVersionBlock(lastBlock, blockNumber, blockSize, blockData)
                
                if retVal <> 0x00us then
                    this.nakResponse(message, retVal)
                else
                    this.ackResponse(message)
        
        
        (* Helper methods *)
        //We need to convert the DateTime instance to a byte[] understood by the device "yyyymmddhhmmss"
        member private this.getTimeStampBytes (date : DateTime) =
            let stringNumberToByte s = Byte.Parse(s.ToString()) //Casting to (byte) would give different results
         
            let yearString = date.Year.ToString("0000")
            let monthString = date.Month.ToString("00")
            let dayString = date.Day.ToString("00")
            let hourString = date.Hour.ToString("00")
            let minuteString = date.Minute.ToString("00")
            let secondsString = date.Second.ToString("00")
            
            let y1 = stringNumberToByte yearString.[0]
            let y2 = stringNumberToByte yearString.[1]
            let y3 = stringNumberToByte yearString.[2]
            let y4 = stringNumberToByte yearString.[3]  
            let m1 = stringNumberToByte monthString.[0]
            let m2 = stringNumberToByte monthString.[1]
            let d1 = stringNumberToByte dayString.[0]
            let d2 = stringNumberToByte dayString.[1]
            let h1 = stringNumberToByte hourString.[0]
            let h2 = stringNumberToByte hourString.[1]
            let min1 = stringNumberToByte minuteString.[0]
            let min2 = stringNumberToByte minuteString.[1]
            let s1 = stringNumberToByte secondsString.[0]
            let s2 = stringNumberToByte secondsString.[1]
    
            [| y1 ; y2 ; y3 ; y4 ; m1 ; m2 ; d1 ; d2 ; h1 ; h2 ; min1 ; min2 ; s1; s2 |]
    
        //Sets the high bit of a byte to 1
        member private this.setHigh (b : byte) : byte = 
            let array = new BitArray([| b |])
            array.[7] <- true
            let mutable converted = [| 0 |]
            array.CopyTo(converted, 0);
            (byte)converted.[0]
    
        //Build the header of a Message based on Intro + Data
        member private this.buildHeader (intro : MessageIntro) (data : byte[]) =
            let headerLength = 7;
            let introLength = 7;
            let length = (uint16)(headerLength + introLength + data.Length)
            let crcData = ByteArrayExtender.Concat(intro.GetRawData(),data)
            let crcValue = ByteArrayExtender.CalculateCRC16(crcData)
            let lengthBytes = UShortExtender.ToIntelOrderedByteArray(length);
            let crcValueBytes = UShortExtender.ToIntelOrderedByteArray(crcValue);
            let headerChecksum = (byte)(SYNC_BYTE ^^^ lengthBytes.[0] ^^^ lengthBytes.[1] ^^^ crcValueBytes.[0] ^^^ crcValueBytes.[1])
            let header = new MessageHeader();
            header.Sync <- SYNC_BYTE
            header.Length <- length
            header.HeaderChecksum <- headerChecksum
            header.DataChecksum <- crcValue
            header
            
        member private this.getStatusBytes =
            let l = controller.Status
            let status = (uint16)controller.Status
            let statusBytes = BitConverter.GetBytes(status);
            statusBytes |> Array.rev
            
    end




(Please note that in the real source, the classes have different names, more specific than "Hardware")


I'm hoping for suggestions, ways to improve the code or even different ways to handle the problem.
For example, would the use of a dynamic language such as IronPython make things easier,
am I going at the the wrong way all together. What's your experience with problems like this,
what would you change, avoid, etc....




**Update:**

Based on the answer by Brian, I written down the following:

    type DrCode9Item = {Name : string ; Timestamp : DateTime ; Size : uint32; Status : byte}
    type DrCode11Item = {Id : byte ; X : uint16 ; Y : uint16 ; SizeX : uint16 ; SizeY : uint16
                         Font : string ; Alignment : byte ; Scroll : byte ; Flash : byte}
    type DrCode12Item = {Id : byte ; X : uint16 ; Y : uint16 ; SizeX : uint16 ; SizeY : uint16}
    type DrCode14Item = {X : byte ; Y : byte}
    
    type DRType =
    | DrCode0 of byte
    | DrCode1 of byte * string * DateTime * uint32 * byte
    | DrCode2 of byte * byte * uint16 * uint16 * array<byte>
    | DrCode3 of byte * string
    | DrCode4 of byte * string * DateTime * byte * uint16 * array<byte>
    | DrCode5 of byte * uint16
    | DrCode6 of byte * DateTime
    | DrCode7 of byte * uint16 * uint16
    | DrCode8 of byte * uint16 * uint16 * uint16 * array<byte>
    | DrCode9 of byte * uint16 * array<DrCode9Item>
    | DrCode10 of byte * string * DateTime * uint32 * byte * array<byte>
    | DrCode11 of byte * array<DrCode11Item>
    | DrCode12 of byte * array<DrCode12Item>
    | DrCode13 of byte * uint16 * byte * uint16 * uint16 * string * byte * byte
    | DrCode14 of byte * array<DrCode14Item>

I could continue doing this for all the DR types (quite a few),
but I still don't understand how that would help me. I've read
about it on Wikibooks and in Foundations of F# but something is not clicking in my head yet.

**Update 2**

So, I understand I could do the following:

    let execute dr =
        match dr with
        | DrCode0(drCode) -> printfn "Do something"
        | DrCode1(drCode, name, timestamp, size, options) -> printfn "Show the size %A" size
        | _ -> ()
    let date = DateTime.Now
    
    let x = DrCode1(1uy,"blabla", date, 100ul, 0uy)

But when the message comes into the IMessageProcessor,
the choise is made right there what kind of message it is
and the proper function is then called. The above would just
be additional code, at least that is how understand it,
so I must really be missing the point here... but I don't see it.
    
    execute x

