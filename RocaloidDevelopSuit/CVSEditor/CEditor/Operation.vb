﻿'
' Created by SharpDevelop.
' Sleepwalking
'
'
Imports CVSCommon
Imports System.Threading
Imports System.Runtime.InteropServices
Imports System.Timers

Public Enum SoundFlags As Integer
    SND_SYNC = &H0
    SND_ASYNC = &H1
    SND_MEMORY = &H4
    SND_LOOP = &H8
    SND_NOSTOP = &H10
    SND_PURGE = &H40
    SND_APPLICATION = &H80
    SND_NOWAIT = &H2000
    SND_ALIAS = &H10000
    SND_FILENAME = &H20000
End Enum

Public Class Operation
	Public Shared MainCVS As CVS
	
	Public Declare Function PlaySound Lib "winmm.dll" (ByVal pszSound As IntPtr, ByVal homd As IntPtr, ByVal fdwSound As SoundFlags) As Boolean
	Public Declare Function PlaySound Lib "winmm.dll" (ByVal pszSound As String, ByVal homd As IntPtr, ByVal fdwSound As SoundFlags) As Boolean
	
	Public Shared SoundBuffer As IntPtr
	Public Shared SoundCounter As Integer = 0
	Public Shared SoundCounter96 As Integer = 0
	Public Shared SoundLength As Integer
	Public Shared SoundStartTime As DateTime
	Public Shared SoundIsPlaying As Boolean = False
	Public Shared MainNoteBox As NoteBox = MainForm.MainNoteBox
	Public Shared MainScrollBar As HScrollBar = MainForm.NBoxScrollBar
	Public Shared SynthesizingSegment As Boolean
	Public Shared SynthesisSegmentNum As Integer
	
	Public Shared RTSynthesisThread As New Thread(AddressOf Scheduler.RunSynthesizer)
	Public Shared CVESynthesisThread As New Thread(AddressOf CybervoiceEngine.Scheduler.RunSynthesizer)
	Public Shared WithEvents UpdateBarTimer As New System.Timers.Timer(40)
	
	Public Shared Editor_CVSFile As String
	
	Public Shared Sub KeepBar()
		Dim TimePassed As TimeSpan
		If SoundIsPlaying Then
			TimePassed = (DateTime.Now - SoundStartTime)
			MainNoteBox.SelectBar = TimePassed.TotalMilliseconds / 1000 + Scheduler.StartSynthesize / 96000
			If MainNoteBox.SelectBar > MainNoteBox.RBound Then
				MainNoteBox.LBound = MainNoteBox.RBound
				'CONSOLE
				My.Forms.Console.Send("  Music bar reaches next page.")
			End If
			If SynthesizingSegment Then
				If MainNoteBox.SelectBar > CVSOperation.GetEndTime(MainCVS.SegmentList(SynthesisSegmentNum)) Then
					StopSynthesis()
					MainNoteBox.DragNoteNum = SynthesisSegmentNum
				End If
			Else
				If MainNoteBox.SelectBar > CVSOperation.GetEndTime(MainCVS.SegmentList(MainCVS.SegmentListQ)) Then
					StopSynthesis()
				End If
			End If
			MainNoteBox.Redraw()
		End If
	End Sub
	
	Public Shared Sub CVESynthesis(ByVal Path As String)
		CybervoiceEngine.Scheduler.Init()
		CybervoiceEngine.Scheduler.CVS_ = MainCVS
		CybervoiceEngine.MixerWriterEffector.TotalCount = CInt(CVSOperation.GetCVSLength(MainCVS) * 96000)
		CybervoiceEngine.MixerWriterEffector.WriterCounter = 0
		CybervoiceEngine.Scheduler.SetFileOutput(Path)
		CVESynthesisThread = New Thread(AddressOf CybervoiceEngine.Scheduler.RunSynthesizer)
		CVESynthesisThread.Start()
		SoundStartTime = DateTime.Now
		SynthesisForm.Show()
	End Sub
	
	Public Shared Sub StartSynthesis(ByVal Optional StartTime As Double = 0)
		If SoundIsPlaying Then Exit Sub
		
		PlaySound(IntPtr.Zero, IntPtr.Zero, SoundFlags.SND_ASYNC Or SoundFlags.SND_FILENAME)
		
		RTSynthesisThread = New Thread(AddressOf Scheduler.RunSynthesizer)
		Scheduler.CVS_ = MainCVS
		
		'CONSOLE
		My.Forms.Console.Send("  Synthesis thread created.")
		
		Dim StartSegment As Integer = CVSOperation.GetSegment(MainCVS, StartTime)
		Dim PreSynthesis As Integer = CInt(96000 * CVSOperation.GetTimeSinceSegment(MainCVS.SegmentList(StartSegment), StartTime))
		
		'CONSOLE
		My.Forms.Console.Send("  Start segment = " & StartSegment)
		My.Forms.Console.Send("  PreSynthesis = " & PreSynthesis)
		
		Scheduler.NowSynthesizing = StartSegment
		Scheduler.StartSynthesize = CInt(StartTime * 96000)
		SoundCounter = 0
		MixerWriterEffector.SynthesisMode = SynthesisState.PreSynthesis
		MixerWriterEffector.SynthesisDestCounter = PreSynthesis
		
		'CONSOLE
		My.Forms.Console.Send("  Scheduler -> Start Synthesize = " & Scheduler.StartSynthesize)
		My.Forms.Console.Send("  MixerWriterEffector -> SynthesisDestCounter = " & MixerWriterEffector.SynthesisDestCounter)
		
		RTSynthesisThread.Start()
		
		'CONSOLE
		My.Forms.Console.Send("  Synthesis thread started.")
		
		While MixerWriterEffector.SynthesisDestCounter > 0
			Threading.Thread.Sleep(1)
			'CONSOLE
			My.Forms.Console.Send("  Waiting for pre synthesis...")
			My.Forms.Console.Send("    MixerWriterEffector -> SynthesisDestCounter = " & MixerWriterEffector.SynthesisDestCounter)
		End While
		
		SoundCounter = 0
		SoundCounter96 = 0
		SoundStartTime = DateTime.Now
		SoundStartTime.AddMilliseconds(-PreSynthesis / 96000 * 1000)
		MixerWriterEffector.SynthesisMode = SynthesisState.RTSynthesis
		
		'CONSOLE
		My.Forms.Console.Send("  SoundStartTime = " & SoundStartTime)
		
		While SoundCounter96 < 10000
			Threading.Thread.Sleep(1)
			'CONSOLE
			My.Forms.Console.Send("  Waiting for synthesizing head...")
			My.Forms.Console.Send("    SoundCounter96 = " & SoundCounter96)
		End While
		
		SoundStartTime = DateTime.Now
		SoundStartTime.AddMilliseconds(-PreSynthesis / 96000 * 1000)
		
		'CONSOLE
		My.Forms.Console.Send("  SoundStartTime = " & SoundStartTime)
		
		PlaySound(SoundBuffer, IntPtr.Zero, SoundFlags.SND_ASYNC Or SoundFlags.SND_MEMORY Or SoundFlags.SND_LOOP)
		SynthesizingSegment = False
		SoundIsPlaying = True
		MainNoteBox.EditEnabled = False
		MainNoteBox.SNoteBox.EditEnabled = False
		
		'CONSOLE
		My.Forms.Console.Send("  Sound started.")
	End Sub
	
	Public Shared Sub StartSegmentSynthesis(ByVal Optional StartTime As Double = 0)
		If SoundIsPlaying Then Exit Sub
		
		PlaySound(IntPtr.Zero, IntPtr.Zero, SoundFlags.SND_ASYNC Or SoundFlags.SND_FILENAME)
		
		RTSynthesisThread = New Thread(AddressOf Scheduler.RunSegmentSynthesis)
		Scheduler.CVS_ = MainCVS
		
		'CONSOLE
		My.Forms.Console.Send("  Synthesis thread created.")
		
		Dim StartSegment As Integer = CVSOperation.GetSegment(MainCVS, StartTime)
		Dim PreSynthesis As Integer = 0
		
		'CONSOLE
		My.Forms.Console.Send("  Start segment = " & StartSegment)
		My.Forms.Console.Send("  PreSynthesis = " & PreSynthesis)
		
		Scheduler.NowSynthesizing = StartSegment
		SynthesisSegmentNum = StartSegment
		Scheduler.StartSynthesize = CInt(StartTime * 96000)
		SoundCounter = 0
		MixerWriterEffector.SynthesisMode = SynthesisState.PreSynthesis
		MixerWriterEffector.SynthesisDestCounter = PreSynthesis
		
		'CONSOLE
		My.Forms.Console.Send("  Scheduler -> Start Synthesize = " & Scheduler.StartSynthesize)
		My.Forms.Console.Send("  MixerWriterEffector -> SynthesisDestCounter = " & MixerWriterEffector.SynthesisDestCounter)
		
		RTSynthesisThread.Start()
		
		'CONSOLE
		My.Forms.Console.Send("  Synthesis thread started.")
		
		While MixerWriterEffector.SynthesisDestCounter > 0
			Threading.Thread.Sleep(1)
			'CONSOLE
			My.Forms.Console.Send("  Waiting for pre synthesis...")
			My.Forms.Console.Send("    MixerWriterEffector -> SynthesisDestCounter = " & MixerWriterEffector.SynthesisDestCounter)
		End While
		
		SoundCounter = 0
		SoundCounter96 = 0
		SoundStartTime = DateTime.Now
		SoundStartTime.AddMilliseconds(-PreSynthesis / 96000 * 1000)
		MixerWriterEffector.SynthesisMode = SynthesisState.RTSynthesis
		
		'CONSOLE
		My.Forms.Console.Send("  SoundStartTime = " & SoundStartTime)
		
		While SoundCounter96 < 10000
			Threading.Thread.Sleep(1)
			'CONSOLE
			My.Forms.Console.Send("  Waiting for synthesizing head...")
			My.Forms.Console.Send("    SoundCounter96 = " & SoundCounter96)
		End While
		
		SoundStartTime = DateTime.Now
		SoundStartTime.AddMilliseconds(-PreSynthesis / 96000 * 1000)
		
		'CONSOLE
		My.Forms.Console.Send("  SoundStartTime = " & SoundStartTime)
		
		PlaySound(SoundBuffer, IntPtr.Zero, SoundFlags.SND_ASYNC Or SoundFlags.SND_MEMORY Or SoundFlags.SND_LOOP)
		SynthesizingSegment = True
		SoundIsPlaying = True
		MainNoteBox.EditEnabled = False
		MainNoteBox.SNoteBox.EditEnabled = False
		
		'CONSOLE
		My.Forms.Console.Send("  Sound started.")
	End Sub
	
	Public Shared Sub StopSynthesis()
		'CONSOLE
		My.Forms.Console.Send("  Aborting synthesis thread...")
		Try
			SyncLock CybervoiceEngine.CVDBContainer.LoaderLock
				RTSynthesisThread.Abort()
			End SyncLock
			'CONSOLE
			My.Forms.Console.Send("  Synthesis thread was aborted successfully.")
		Catch
			'CONSOLE
			My.Forms.Console.Send("  [Error] Failed to abort the synthesis thread.")
		End Try
		
		PlaySound(IntPtr.Zero, IntPtr.Zero, SoundFlags.SND_ASYNC Or SoundFlags.SND_MEMORY)
		
		'CONSOLE
		My.Forms.Console.Send("  Sound stopped.")
		
		SoundIsPlaying = False
		MainNoteBox.EditEnabled = True
		MainNoteBox.SNoteBox.EditEnabled = True
		CybervoiceEngine.Scheduler.CloseAll()
		
		'CONSOLE
		My.Forms.Console.Send("  Scheduler -> CloseAll.")
	End Sub
	
	Public Shared Sub SynthesizerInit()
		CybervoiceEngine.Compatibility_DataDir = "E:\DSync\Dropbox\RocaBeautiful\CVE1.6\CybervoiceEngine\bin\Debug\Data\"
		Scheduler.Init()
		CybervoiceEngine.Scheduler.Init()
	End Sub
	
	Public Shared Sub SoundInit()
		SoundLength = 220500
		SoundBuffer = Marshal.AllocHGlobal(441044)
		MixerWriterEffector.WriteWaveHeader(SoundBuffer, SoundLength)
		AddHandler UpdateBarTimer.Elapsed, AddressOf KeepBar
		UpdateBarTimer.Start()
	End Sub
	
	Public Shared Sub LoadCVS(ByVal Path As String)
		Reader_Open(Path)
		CVS_Read(MainCVS)
		Reader_Close()
	End Sub
	
	Public Shared Sub SaveCVS(ByVal Path As String)
		Writer_Save(Path)
		CVS_Write(MainCVS)
		Writer_Close()
	End Sub
End Class
