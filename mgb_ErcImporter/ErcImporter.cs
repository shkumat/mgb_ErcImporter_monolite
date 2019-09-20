// Версия 3.03 от 29.08.2019г. Загрузка в `Скрудж` платежных документов из А-файлов
//
// v3.2  - добавлено запись ошибок в таблицу Mega_Opengate_Logs
// v3.1  - добавлена поддержка измененной структуры А-файла с учетом IBAN и "длинных" номеров счетов
using	MyTypes;
// список режимов работы программы
public sealed class WorkModes {
	public	const	string	OPENGATE	=	"/A"	; 	// Универсальный шлюз
	public	const	string	CHECK		=	"/C"	;	// только проверка
	public	const	string	DISABLE		=	"/D"	;	// `забраковать` заготовки из А-файла
	public	const	string	SAKR		=	"/S"	;	// САКР
	public	const	string	ERC		=	"/U"	;	// ЕРЦ(коммуналка)
	public	const	string	SALARY		=	"/Z"	;	// Энигма
}

// переопределение вычитывателя конфигурации, если загружаем зарплату
public	class	CSalaryConfig : CErcConfig {
	public	override string	TodayDir()
	{
		string TmpS = CCommon.DtoC(Erc_Date);
		TmpS = TmpS.Substring(6, 2) + TmpS.Substring(4, 2) + TmpS.Substring(2, 2);
		return CfgFile["SalaryDir"] + "\\" + TmpS + "\\";
	}
}

// переопределение вычитывателя конфигурации, если выбран универсальный шлюза
public	class	COpenwayConfig	:	CErcConfig {
	public	override string StatDir()
	{
		return TodayDir() + "\\STA\\";
	}
        public	override string Config_FileName()
        {
        	return	"EXE\\GLOBAL.FIL";
        }
	public	override string	TodayDir()
	{
		string TmpS = CCommon.DtoC(Erc_Date);
		return CfgFile["DaysDir"] + "\\" + TmpS.Substring(2, 6) + "\\";
	}
}

// загрузка допреквизитов из файла
public	class	CErcEFile {
	CErcEReader	EFileReader	;

	public	CErcEFile()
	{
		EFileReader	= new	CErcEReader();
	}

	public	bool	Load(CCommand Command , string FileName )
	{
		string		CmdText		=	"";
		bool		Result		=	true;
		if	( EFileReader.Open( FileName ,CAbc.CHARSET_DOS) ) {
			while ( ( EFileReader.Read() ) && ( Result ) ) {
				CmdText		=	"exec   dbo.pMega_OpenGate_Params "
						+	"       @FileName       =  '!A" +  CCommon.GetFileName( FileName ).Substring(2,10) + "'"
						+	" ,     @LineNum        =     " +  EFileReader[ CErcEFileInfo.L_LINENUM ]
						+	" ,     @Code           =    '" +  EFileReader[ CErcEFileInfo.L_PARAMCODE ]         + "'"
						+	" ,     @Info           =    '" +  EFileReader[ CErcEFileInfo.L_INFO ]              + "'";
				if(	Command.Execute( CmdText )	!=	true	)
					Result	=	false;
			}
		}
		EFileReader.Close();
                return	Result;
	}
}

// считывание и сохранение настроек программы
class	CAppSettings {

	const int	TOTAL_GROUP	= 3 ; // кол-во груп настроек
	string[]	Topics		;
	string[]	Purposes	;
	string[]	TopicMenus	;
	int		Current		;
	CConnection	Connection	;

	public	CAppSettings( CConnection Cnct ) {
		Connection	=	Cnct;
		Topics		= new	string[ TOTAL_GROUP ] ;
		Purposes	= new	string[ TOTAL_GROUP ] ;
		TopicMenus	= new	string[ TOTAL_GROUP ] ;
		for	( Current=0; Current<TOTAL_GROUP; Current++ )
		{
			Topics[ Current ]	=	"" ;
			TopicMenus[ Current ]	= 	"" ;
			Purposes[ Current ]	=	"" ;
		}
	}

	void	Print() {
		CConsole.Clear();
		for	( Current=0; Current<TOTAL_GROUP; Current++ )
		{
			if	( Topics[ Current ] == "" )
				TopicMenus[ Current ] = " ( пусто ) ";
			else	TopicMenus[ Current ] = " Для счетов "+Topics[ Current ].Trim();
		}
		for	( Current=0; Current<TOTAL_GROUP; Current++ )
			if	( Topics[ Current ] != "" )
				CCommon.Print( TopicMenus[ Current ] + " : " + Purposes[ Current ] + CAbc.CRLF );
	}

	public	bool	Save() {
		if	( Connection == null )
			return	false;
		CCommand	Command		= new	CCommand( Connection );
		string		CmdText		=	""		;
		bool		Result		=	true		;
		for	( Current=0; Current<TOTAL_GROUP; Current++ )
		{
			CmdText		=	"declare @OwnerCode as VarChar(32) select @OwnerCode=user_name() exec dbo.Mega_Common_Registry;2 @OwnerCode=@OwnerCode , @TaskCode='loader' , @TaskName='TAL\\FILIAL\\LOADER.EXC' ";
			if	( Current == 0 )
				CmdText		=	CmdText + ", @FlagCode = '' ";
			else	CmdText		=	CmdText + ", @FlagCode = '"+ Current.ToString() +"' ";
			CmdText		=	CmdText + ", @Name = '" + Topics[ Current ] + "' " ;
			CmdText		=	CmdText + ", @Info = '" + Purposes[Current]+ "' " ;
			if(	Command.Execute( CmdText )	!=	true	)
				Result	=	false;
		}
		Command.Close();
                return	Result;
	}

	public	bool	Load() {
		if	( Connection == null )
			return	false;
		CRecordSet	RecordSet	= new	CRecordSet( Connection ) ;
		if	( RecordSet.Open("declare @OwnerCode as VarChar(32) select @OwnerCode=user_name() exec dbo.Mega_Common_Registry;1 @OwnerCode=@OwnerCode , @TaskCode='loader' , @TaskName='TAL\\FILIAL\\LOADER.EXC' ") )
			while	( RecordSet.Read() )
				if	( CCommon.IsDigit( "0" + RecordSet["FlagCode"].Trim() ) )
				{
					Current = CCommon.CInt32( "0" + RecordSet["FlagCode"].Trim() ) ;
					if	( Current < TOTAL_GROUP )
					{
						Topics[ Current ] = RecordSet["Name"].Trim() ;
						Purposes[ Current ] = RecordSet["Info"].Trim() ;
					}
				}
		RecordSet.Close();
		Print();
		Current	=	CConsole.GetMenuChoice("Принять такие назначения платежей","Изменить назначения платежей");
		while	( Current==2 )
		{
			Print();
			Current	=	CConsole.GetMenuChoice( TopicMenus );
			while	( Current != 0 )
			{
				CCommon.Write( " Введите номер балансового счета или список через запятую : ");
				Topics[ Current-1 ] = CCommon.Input().Trim();
				CCommon.Write( CAbc.CRLF+" Назначение платежа : ");
				Purposes[ Current-1 ] = CCommon.Input().Trim();
				Print();
				Current	=	CConsole.GetMenuChoice( TopicMenus );
			}
			Print();
			Current	=	CConsole.GetMenuChoice("Принять такие назначения платежей","Изменить назначения платежей");
		}
		if	( Current == 1 )
			return	true;
		else	return	false;
	}
}

// проверка и загрузка файла с платежами
public	class	CErcAFile
{
	CSepAReader	AFileReader			;
	string		CmdText				;
	long		TotalCredit	=	0	;
	int		StringCount	=	0	;
	readonly	string	USER_NAME	=	CCommon.Upper( CCommon.GetUserName() ) ;

	public	CErcAFile() {
		AFileReader = new	CSepAReader()	;
	}

	public	System.Decimal	Total_Credit
	{
		get
        	{	System.Decimal	Result	=	TotalCredit;
        		return	Result/100;
        	}
	}

	public	int	String_Count
	{
		get
        	{
        		return	StringCount;
        	}
	}

	public	bool	UserAccepted( string FileName )
	{
        	string	ShortName	=	CCommon.GetFileName( FileName );
       		string[]	PreviewLines	=
				{	"  Начальные строки Файла ..."
				,	"___________________________________________________"
				,	""
				,	""
				,	""
				,	""
				,	""
				,	""
				,	""
				,	""
				,	""
                		,	""
                		,	""
                		,	"___________________________________________________"
                		,	" Для загрузки файла нажмите Enter. Для отмены - Esc"
                		};
		if (AFileReader.Open(FileName, CAbc.CHARSET_DOS))
		{
			TotalCredit		=	CCommon.CInt64( AFileReader.Head(CSepAFileInfo.H_TOTALCREDIT).Trim() );
			PreviewLines[0]		=	ShortName + " : " +	AFileReader.Head(CSepAFileInfo.H_STRCOUNT).Trim()
                        			+ 	" строк, общая сумма" + CCommon.StrN(Total_Credit,11).Replace(",",".") ;
			int	I=3;
			while ( (AFileReader.Read()) && (I<PreviewLines.Length-2) )
			{
				PreviewLines[I]	=		CCommon.Left( AFileReader[CSepAFileInfo.L_DEBITMFO].Trim() , 6  )
                                		+" : "	+	CCommon.Left( AFileReader[CSepAFileInfo.L_DEBITACC].Trim() , 14 )
                                                +" >>  "+	CCommon.Left( AFileReader[CSepAFileInfo.L_CREDITMFO].Trim() , 6 )
                                                +" : "	+	CCommon.Left( AFileReader[CSepAFileInfo.L_CREDITACC].Trim() , 14 );
                                I++;
                        }
		}
		else
		{
			AFileReader.Close();
			return	false;
		}
		AFileReader.Close();
			return	CConsole.GetBoxChoice( PreviewLines );
	}

	public	string	Check( CCommand Command, string SourceFileName ) {
			string	Result		=	""	;
			string	AboutError	=	""	;
        	bool	HaveError	=	false	;
                string	ShortFileName	=	CCommon.GetFileName( SourceFileName  )	;

        	TotalCredit	=	0;
        	StringCount	=	0;
		if	( AFileReader.Open( SourceFileName , CAbc.CHARSET_DOS) ) {
			HaveError	=	false;
			while ( AFileReader.Read() ) {
				TotalCredit	+=	CCommon.CInt64( AFileReader[CSepAFileInfo.L_SUMA].Trim() )	;
                                StringCount	++	;
				AboutError	=	"" ;
				CmdText		=	"exec   dbo.pMega_OpenGate_CheckPalvis"
							+	" @Code         = '"+ AFileReader[CSepAFileInfo.L_NDOC].Replace("'","`").Trim() +"'"
							+	",@Ctrls        = '"+ AFileReader[CSepAFileInfo.L_NDOC].Replace("'","`").Trim() +"'"
							+	",@SourceCode   = '"+ AFileReader[CSepAFileInfo.L_DEBITMFO].Replace("'","`").Trim() +"'"
							+	",@DebitMoniker = '"+ AFileReader[CSepAFileInfo.L_DEBITACC].Replace("'","`").Trim() +"'"
							+	",@DebitState   = '"+ AFileReader[CSepAFileInfo.L_OKPO1].Replace("'","`").Trim() +"'"
							+	",@DebitIBAN    = '"+ AFileReader[CSepAFileInfo.L_DEBITIBAN].Replace("'","`").Trim() +"'"
							+	",@TargetCode   = '"+ AFileReader[CSepAFileInfo.L_CREDITMFO].Replace("'","`").Trim() +"'"
							+	",@CreditMoniker= '"+ AFileReader[CSepAFileInfo.L_CREDITACC].Replace("'","`").Trim() +"'"
							+	",@CreditState  = '"+ AFileReader[CSepAFileInfo.L_OKPO2].Replace("'","`").Trim() +"'"
							+	",@CreditIBAN   = '"+ AFileReader[CSepAFileInfo.L_CREDITIBAN].Replace("'","`").Trim() +"'"
							+	",@CrncyAmount  =  "+ AFileReader[CSepAFileInfo.L_SUMA].Replace("'","`").Trim()
							+	",@CurrencyId   =  "+ AFileReader[CSepAFileInfo.L_CURRENCY].Replace("'","`").Trim()
							+	",@FileName     = '"+ ShortFileName.Replace("'","`").Trim()+"'" 
							+	",@LineNum      =  "+ StringCount.ToString()
							+	",@UserName	= '"+ USER_NAME + "'"
							;
				AboutError	=	(string) Command.GetScalar( CmdText );
				if	( AFileReader[CSepAFileInfo.L_PURPOSE].Trim() == "" )
					AboutError	+=	" Не заполнено назначение платежа ;" ;
				if	( AFileReader[CSepAFileInfo.L_DEBITNAME].Trim() == "" )
					AboutError	+=	" Не заполнено название дб. счета ;" ;
				if	( AFileReader[CSepAFileInfo.L_CREDITNAME].Trim() == "" )
					AboutError	+=	" Не заполнено название кт. счета ;" ;
				if	( AboutError != null )
					if	( ( AboutError.Trim() != "" ) ) {
							HaveError	=	true;
							Result		=	Result+" Ошибка в строке " + StringCount.ToString() +" : " + AboutError.Trim()  + CAbc.CRLF;
						}
				CConsole.ShowBox(""," Проверяется строка" + CCommon.StrI( StringCount , 5 ) + " " ,"")	;
			}
			CConsole.Clear();
			if	( HaveError )
				Result		=	Result + "Ошибка в реквизитах платежей !" + CAbc.CRLF ;
			if	( TotalCredit != CCommon.CInt64( AFileReader.Head(CSepAFileInfo.H_TOTALCREDIT).Trim() ) )
				Result		=	Result + CAbc.CRLF + "Неверная итоговая сумма платежей !" + CAbc.CRLF	;
			if	( CCommon.IsDigit( "0" + AFileReader.Head(CSepAFileInfo.H_STRCOUNT).Trim() )  == false )
				Result		=	Result + CAbc.CRLF + "Неверное общее количество строк !" + CAbc.CRLF	;
			else
				if	( StringCount != CCommon.CInt32( "0" + AFileReader.Head(CSepAFileInfo.H_STRCOUNT).Trim() ) )
					Result		=	Result + CAbc.CRLF + "Неверное общее количество строк !" + CAbc.CRLF ;
		}
        else
			Result	+=	"Ошибка чтения файла !" + CAbc.CRLF;
		AFileReader.Close();
		byte	SavedColor		=	CConsole.BoxColor;
		if	( ( ( int ) CCommon.IsNull( Command.GetScalar( "exec dbo.pMega_OpenGate_CheckPalvis;2 @TaskCode='OpenGate',@FileName='" + ShortFileName + "'" ) , (int) 0 ) ) > 0 )
		{
			CConsole.BoxColor	=	CConsole.RED*16 + CConsole.WHITE	;
			CConsole.GetBoxChoice( "Внимание ! Файл " + ShortFileName + " сегодня уже загружался !" , "" ,"Нажмите Enter.") ;
			HaveError	=	true;
			Result		+=	"Файл " + ShortFileName + " сегодня уже загружался !" + CAbc.CRLF;
			CConsole.BoxColor	=	SavedColor	;
		}
		return	Result	;
	}

	public bool Load( CCommand Command , string FileName , string BranchCode , string TaskCode )
	{
		int	LineNum		;
		bool	Result		=	true;
		string	DebitAcc,CreditAcc,DebitIBAN,CreditIBAN;
		if (AFileReader == null)
			return false;
		if (AFileReader.Open(FileName, CAbc.CHARSET_DOS)) {
			string	ShortFileName	=	CCommon.GetFileName( FileName ) ;
			LineNum			=	1;
			while (AFileReader.Read()) {
				DebitAcc	=	AFileReader[CSepAFileInfo.L_DEBITACC].Replace("'","`").Trim();
				DebitAcc	=	CCommon.IsDigit( DebitAcc ) ? DebitAcc : AFileReader[CSepAFileInfo.L_DEBITACC_EXT].Replace("'","`").Trim();
				DebitAcc	=	CCommon.IsDigit( DebitAcc ) ? DebitAcc : "";
				CreditAcc	=	AFileReader[CSepAFileInfo.L_CREDITACC].Replace("'","`").Trim();
				CreditAcc	=	CCommon.IsDigit( CreditAcc ) ? CreditAcc : AFileReader[CSepAFileInfo.L_CREDITACC_EXT].Replace("'","`").Trim();
				CreditAcc	=	CCommon.IsDigit( CreditAcc ) ? CreditAcc : "" ;
				DebitIBAN	=	AFileReader[CSepAFileInfo.L_DEBITIBAN].Replace("'","`").Trim();
				DebitIBAN	=	CCommon.IsLetter( DebitIBAN ) ? DebitIBAN : CAbc.EMPTY  ;
				CreditIBAN	=	AFileReader[CSepAFileInfo.L_CREDITIBAN].Replace("'","`").Trim();
				CreditIBAN	=	CCommon.IsLetter( CreditIBAN ) ? CreditIBAN : CAbc.EMPTY ;
				CmdText		=	"exec  dbo.pMega_OpenGate_AddPalvis "
						+	" @TaskCode     = '" + TaskCode.Trim() +"'"
						+	",@BranchCode   = '" + BranchCode.Trim() + "'"
						+	",@FileName     = '" + ShortFileName + "'"
						+	",@LineNum      =  " + LineNum.ToString()
						+	",@Code         = '" + AFileReader[CSepAFileInfo.L_NDOC].Replace("'","`").Trim()	+ "'"
						+	",@Ctrls        = '" + AFileReader[CSepAFileInfo.L_SYMBOL].Replace("'","`").Trim()	+ "'"
						+	",@SourceCode   = '" + AFileReader[CSepAFileInfo.L_DEBITMFO].Replace("'","`").Trim()	+ "'"
						+	",@DebitMoniker = '" + DebitAcc	+ "'"
						+	",@DebitName    = '" + AFileReader[CSepAFileInfo.L_DEBITNAME].Replace("'","`").Trim()	+ "'"
						+	",@DebitState   = '" + AFileReader[CSepAFileInfo.L_OKPO1].Replace("'","`").Trim()	+ "'"
						+	",@DebitIBAN    = '" + DebitIBAN + "'"
						+	",@TargetCode   = '" + AFileReader[CSepAFileInfo.L_CREDITMFO].Replace("'","`").Trim()	+ "'"
						+	",@CreditMoniker= '" + CreditAcc	+ "'"
						+	",@CreditName   = '" + AFileReader[CSepAFileInfo.L_CREDITNAME].Replace("'","`").Trim()	+ "'"
						+	",@CreditState  = '" + AFileReader[CSepAFileInfo.L_OKPO2].Replace("'","`").Trim()	+ "'"
						+	",@CreditIBAN   = '" + CreditIBAN + "'"
						+	",@CrncyAmount  =  " + AFileReader[CSepAFileInfo.L_SUMA].Replace("'","`").Trim()
						+	",@CurrencyId   =  " + AFileReader[CSepAFileInfo.L_CURRENCY].Replace("'","`").Trim()
						+	",@Purpose      = '" + AFileReader[CSepAFileInfo.L_PURPOSE].Replace("'","`").Trim()	+ "'"
						+	",@OrgDate      =  " + CCommon.StrDate_To_IntDate( "20" + AFileReader[CSepAFileInfo.L_DATE2].Trim() )
						+	",@UserName		= '" + USER_NAME + "'"
						;
				if	(	Command.Execute( CmdText )	!=	true	)
					Result	=	false;
				LineNum		=	LineNum	+ 1;
				CConsole.ShowBox(""," Загружается строка" + CCommon.StrI( LineNum , 5 ) + " " ,"") ;
			}
		}
		AFileReader.Close();
		return	Result;
	}
}

//  основная программа
public class ErcImporter
{
	static	string		WorkMode	=	""	;
	static	string		TmpDir				;
	static	string		TodayDir			;
	static	string		InputDir			;
	static	int		SeanceNum			;
	static	CCommand	Command				;
	static	CConnection	Connection			;
	static	CErcAFile	ErcAFile			;
	static	CErcEFile	ErcEFile			;
	static	CErcConfig	ErcConfig			;
	static	CAppSettings	AppSettings			;
	static	CScrooge2Config	Scrooge2Config			;
	static	string		LogFileName			;

	//  - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
	//  Получить имя файла с помощью текстового меню
	public	static string	SelectFileName( string FileMask ) {
		if	(FileMask==null)
                	return	"";
        	if	(FileMask.Trim().Length==0)
                	return	"";
		string[]	FileList;
                FileList	=	CCommon.GetFileList( FileMask );
                if(FileList==null)
                	return	"";
                if(FileList.Length==0)
                	return	"";
                int	FileCount	=	0;
                foreach( string FileName in FileList )
			if ( FileName != null )
				if ( FileName.Trim() != "")
                                	if( CCommon.GetFileName( FileName ).IndexOf("_") < 0 )
                                        	FileCount++	;
                if(FileCount<1)
                	return	"";
                string[]	FileNames	= new	string[ FileCount ]	;
                int	I		=	0;
                foreach( string FileName in FileList )
			if ( FileName != null )
				if ( FileName.Trim() != "")
                                	if( ( CCommon.GetFileName( FileName ).IndexOf("_") < 0 ) && (I<FileCount) )
                                               	FileNames[I++]	=	FileName	;
		string[]	MenuItems	= new	string[ FileCount ]	;
                for(I=0;I<FileCount;I++)
                {
                	MenuItems[I]	=	CCommon.GetFileName( FileNames[I] )	;
                	long	FileSize=	CCommon.GetFileSize( FileNames[I] )	;
                        if(FileSize<892)
                        	FileSize=0;
                        else
                        	FileSize	=	(FileSize- 298 )/594 	;
                       	MenuItems[I]	=	CCommon.Left( CCommon.GetFileName( FileNames[I] ),16) + " " + CCommon.Right(FileSize.ToString(),5)+" строк";
                }
                I	=	CConsole.GetMenuChoice( MenuItems )	;
                if	( I > 0 )
                	return	FileNames[I-1]	;
               	return	""	;
	}
	//  - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - 
	//  Получить имя файла с помощью графической панели открытия файла
	static string SelectFileNameGUI( string SettingsPath , string  DestPath ) {
		string		TmpS		=	CAbc.EMPTY;
		string		Result		=	CAbc.EMPTY;
		string		SettingsFileName=	null;
		if	( SettingsPath != null )
			if	( SettingsPath.Trim().Length > 0 ) {
		      		SettingsFileName	=	SettingsPath.Trim() + "\\" + CCommon.GetUserName() + ".ldr";
		      		if	( CCommon.FileExists( SettingsFileName ) )
					TmpS		=	CCommon.LoadText(  SettingsFileName , CAbc.CHARSET_WINDOWS );
				if	( TmpS == null )
				        TmpS	=	CAbc.EMPTY;
			}
		TmpS	=	TmpS.Trim();
		TmpS	=	CCommon.OpenFileBox(
					"Укажите А-файл для загрузки"
				,	TmpS
				,	"А-файлы (?A*.*)|?a*.*"
			);
		if	( TmpS == null )
			return	Result;
		TmpS		=	TmpS.Trim();
                if	( TmpS.Length == 0 )
			return	Result;
		if	( SettingsFileName != null )
			CCommon.SaveText( SettingsFileName , CCommon.GetDirName( TmpS ) , CAbc.CHARSET_WINDOWS ) ;
		Result		=	DestPath.Trim() + "\\" + CCommon.GetFileName( TmpS );
		if	( CCommon.FileExists( Result ) ) {
			CCommon.Print("","Выбранный файл уже существует в целевом каталоге : ",Result,"","Нажмите  ENTER  для выхода...");
			CCommon.Input();
			Result		=	CAbc.EMPTY;
		}
		else
			if	( ! CCommon.CopyFile( TmpS , Result ) ) {
				CCommon.Print("Ошибка копирования файла в целевой каталог : ",Result,"","Нажмите  ENTER  для выхода...");
				CCommon.Input();
				Result		=	CAbc.EMPTY;
			}
		return	Result;
	}
	//  - - - - - - - - - - - - - - - - - - - - - - - - - -
	static	void	ProcessAFile( CCommand Command , string FileName , bool IsCheckNeeded ) {
		string	ShortFileName	=	null	;
		string	BranchCode	=	null	;
		string	TmpFileName	=	null	;
		string	AboutError	=	null	;
		bool	Result		=	false	;
		byte	SavedColor	=	CConsole.BoxColor ;

		if (FileName	==	null)
			return	;
		if (FileName.Trim() ==	"")
			return	;
		ShortFileName		=	CCommon.GetFileName(FileName)	;
		CCommon.AppendText( LogFileName , CCommon.Now() + "   " + CCommon.Upper(CCommon.GetUserName()) + "  загружает файл " + ShortFileName + CAbc.CRLF , CAbc.CHARSET_WINDOWS );

		if	( ShortFileName.IndexOf("_") < 0 )
			BranchCode		=	""		;
		else	BranchCode		=	ShortFileName.Substring( 2,2)	;

		TmpFileName		=	TodayDir + CAbc.SLASH + ShortFileName	;
		if	( ! CCommon.FileExists( TmpFileName ) )
			CCommon.CopyFile( FileName , TmpFileName ) ;
		if	( ! CCommon.FileExists( TmpFileName ) )
			return ;
		TmpFileName		=	TmpDir + CAbc.SLASH
					+	CCommon.Right( "0" + CCommon.Hour(CCommon.Clock()).ToString() , 2 )
					+	CCommon.Right( "0" + CCommon.Minute( CCommon.Clock()).ToString() , 2 )
					+	CCommon.Right( "0" + CCommon.Second( CCommon.Clock()).ToString() , 2 )	;
		if( ! CCommon.DirExists( TmpFileName ) )
			CCommon.MkDir( TmpFileName )	;
		TmpFileName		=	TmpFileName + CAbc.SLASH + ShortFileName	;
		if	( CCommon.FileExists( TmpFileName ) )
			CCommon.DeleteFile( TmpFileName )	;
		if	( CCommon.FileExists( TmpFileName ) )
		{
			CConsole.GetBoxChoice("","Ошибка при удалении файла ",TmpFileName,""," Для выхода нажмите Esc.")	;
			return	;
		}
		CCommon.CopyFile( FileName , TmpFileName )	;
		if	( ! CCommon.FileExists( TmpFileName ) )
		{
			CConsole.GetBoxChoice("","Ошибка при создании файла ",TmpFileName,""," Для выхода нажмите Esc.")	;
			return	;
		}
		Err.LogTo( LogFileName )	;
		CCommon.DeleteFile( FileName )	;
                CConsole.BoxColor	=	SavedColor	;
                if	( IsCheckNeeded )
                {
                	AboutError	=	ErcAFile.Check( Command , TmpFileName )	;
                	CConsole.Clear();
			if	( AboutError !=	null )
				if	( AboutError.Trim() !=	"" )
				{
					CCommon.AppendText( LogFileName , AboutError + CAbc.CRLF , CAbc.CHARSET_WINDOWS );
					CCommon.Print( AboutError ) ;
					CConsole.BoxColor	=	CConsole.RED*16 + CConsole.WHITE	;
					if	( ! CConsole.GetBoxChoice(	"Внимание ! При проверке " + ShortFileName + " обнаружены ошибки !"
										,"","Для отмены загрузки нажмите Esc , для продолжения - Enter"
									)
						)
					{
                                        	CCommon.CopyFile( TmpFileName , FileName  )	;
						CCommon.Print( AboutError ) ;
				                CConsole.BoxColor	=	SavedColor	;
						return	;
					}
				}
                }
                CConsole.BoxColor	=	SavedColor	;
		if	( WorkMode	==	WorkModes.OPENGATE )
               	{
               		Result		=	ErcAFile.Load( Command , TmpFileName , BranchCode , "OpenGate"  ) ;
               		if	( Result ) {
               			// добавление в PayRoll информации о файле
				CConsole.BoxColor	=	SavedColor	;
				CConsole.Clear()	;
            			CConsole.ShowBox(""," Подождите..." ,"") ;
            			Command.Execute( " exec pMega_OpenGate_PayRoll;2 @FileName='"  + CCommon.GetFileName( TmpFileName ) + "'" ) ;
            		}
		}
		else
                {
                	Result		=	ErcAFile.Load( Command , TmpFileName , BranchCode , "ErcGate"  )	;
                }
		if	( Result )
                	CCommon.AppendText( LogFileName , CCommon.Now() + "   загрузка закончена." + CAbc.CRLF , CAbc.CHARSET_WINDOWS );
		else
		{
			CCommon.CopyFile( TmpFileName , FileName  )	;
			CConsole.BoxColor	=	CConsole.RED*16 + CConsole.WHITE	;
			CConsole.GetBoxChoice("","  При загрузке " + ShortFileName + " возникли ошибки !","")  ;
		}
		CConsole.BoxColor	=	SavedColor	;
		CConsole.Clear()	;
		Err.LogToConsole()	;
	}
	//  - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
	//  Функцию Main нужно пометить атрибутом [STAThread], чтоб работал OpenFileBox
	[System.STAThread]
	public static void Main()
	{
		Err.LogToConsole();
		int			ErcDate		=	0			;
		bool		UseErcRobot	=	false		;	// подключаться ли к серверу под логином ErcRobot
		string		FileName	=	CAbc.EMPTY	;
		const string	ROBOT_LOGIN	=	"ErcRobot"	;
		const string	ROBOT_PWD	=	"35162987"	;
		const string	TASK_CODE	=	"OpenGate"	;
		string		StatDir		=	null		;
		string		DataBase	=	null		;
		string		ServerName	=	null		;
		string		ScroogeDir	=	null		;
		string		SettingsDir	=	null		;
		string		ConnectionString=	null		;

		if	( System.Console.BufferHeight > 50 ) {
                	System.Console.WindowHeight = 25 ;
                	System.Console.BufferHeight = 25 ;
		}
		System.Console.Title="Загрузка в `Скрудж` платежей из А-файлов ";
		System.Console.BackgroundColor	=	0		;
		System.Console.Clear()	;
		CCommon.Print( "","  Загрузка в `Скрудж` платежей из А-файлов. Версия 3.03 от 29.08.2019г." );
		if( CCommon.ParamCount() < 2 ) {
			CCommon.Print("  В строке запуска нужно указатьть один из режимов :" ) ;
			CCommon.Print("  /A   диалоговый режим, для загрузки файлов для универсального шлюза ;" ) ;
			CCommon.Print("  /С   проверка одного файла, имя которого задано далее в строке ;" ) ;
			CCommon.Print("  /D   `браковка` заготовок из файла, имя которого задано далее в строке ;" ) ;
			CCommon.Print("  /S   пакетный режим  , для загрузки файлов от САКР ( маска !A??_*.* ) ;" ) ;
			CCommon.Print("  /U   диалоговый режим, для загрузки файлов от ЕРЦ  ( кроме !A??_*.* ) ;" ) ;
			CCommon.Print("  /Z   диалоговый режим, для загрузки файлов зарплаты( маска !A*.Z??  ) ." ) ;
			CCommon.Print("","  /R   дополнительная опция - подключаться под пользователем ErcRobot ." ) ;
			return	;
		}
		if	( CCommon.ParamCount() == 0 )
			return;
		for	( int i= 1 ; i< CCommon.ParamCount() ; i++ )
			if	( CAbc.ParamStr[ i ].Trim().ToUpper() == "/R" ) {
				UseErcRobot	= true;
				System.Console.Title = System.Console.Title + " * ";
			}
			else
				if	( CAbc.ParamStr[ i ].Trim().Substring(0,1)=="/")
					WorkMode	=	CCommon.Upper( CAbc.ParamStr[ i ] ).Trim()  ;
				else
					FileName	=	CAbc.ParamStr[ i ].Trim();
		// - - - - - - - - - - - - - - - - - - - - - - - - - - - -
		// Вычитываем настройки "Скрудж-2"
		ErcAFile	= new	CErcAFile()	;
                ErcEFile	= new	CErcEFile()	;
		Scrooge2Config	= new	CScrooge2Config();
		if	(!Scrooge2Config.IsValid) {
			CCommon.Print( Scrooge2Config.ErrInfo ) ;
			return	;
		}
		ScroogeDir	=	(string)Scrooge2Config["Root"]		;
		SettingsDir	=	(string)Scrooge2Config["Common"]	;
		ServerName	=	(string)Scrooge2Config["Server"]	;
		DataBase	=	(string)Scrooge2Config["DataBase"]	;
		if	( ScroogeDir == null ) {
			CCommon.Print("  Не найдена переменная `Root` в настройках `Скрудж-2` ");
			return;
		}
		if	( ServerName == null ) {
			CCommon.Print("  Не найдена переменная `Server` в настройках `Скрудж-2` ");
			return;
		}
		if	( DataBase == null ) {
			CCommon.Print("  Не найдена переменная `Database` в настройках `Скрудж-2` ");
			return;
		}
                ScroogeDir	=	ScroogeDir.Trim()	;
                if	( SettingsDir != null )
                	SettingsDir	=	ScroogeDir + "\\" + SettingsDir ;
		ServerName	=	ServerName.Trim()	;
		DataBase	=	DataBase.Trim()		;
		CCommon.Print("  Беру настройки `Скрудж-2` здесь :  " + ScroogeDir );
		// - - - - - - - - - - - - - - - - - - - - - - - - - - - -
		// Подключаемся к базе данных
		ConnectionString	=	"Server="	+	ServerName
					+	";Database="	+	DataBase	;
		if	( UseErcRobot )
				ConnectionString	+=	";UID=" + ROBOT_LOGIN + ";PWD=" + ROBOT_PWD + ";" ;
		else
				ConnectionString	+=	";Integrated Security=TRUE;" ;
		Connection		= new	CConnection( ConnectionString );
		if	( Connection.IsOpen() )
			/* CCommon.Print("  Сервер        :  " + ServerName ) */ ;
		else {
			CCommon.Print( "  Ошибка подключения к серверу !" );
			return;
		}
		Command			= new	CCommand(Connection) ;
		if	( Command.IsOpen() )
			/* CCommon.Print("  База данных   :  " + DataBase ) */;
		else {
			CCommon.Print( "  Ошибка подключения к базе данных !" );
			return;
		}
		System.Console.Title=System.Console.Title+" | "+ServerName+"."+DataBase	;
		// - - - - - - - - - - - - - - - - -
		// считываем настройки шлюза в ЕРЦ
		AppSettings		= new	CAppSettings( Connection );
		ErcDate			=	( int ) CCommon.IsNull( Command.GetScalar( " exec  dbo.pMega_OpenGate_Days;7 " ) , (int) 0 );
		if	( ErcDate < 1 ) {
			CCommon.Print( " Ошибка определения даты текущего рабочего дня. " );
			return	;
		}
		switch	( WorkMode )  {
        	        case	WorkModes.CHECK		:
                        {
                        	SeanceNum	=	1 ;
							ErcConfig	= new	COpenwayConfig();
							ErcConfig.Open( ErcDate );
							InputDir	=	(string)ErcConfig["InputDir"]	;
							break;
                        }
        	        case	WorkModes.DISABLE	:
                        {
                        	SeanceNum	=	1 ;
							ErcConfig	= new	COpenwayConfig();
							ErcConfig.Open( ErcDate );
							InputDir	=	(string)ErcConfig["InputDir"]	;
							break;
                        }
                	case	WorkModes.SALARY	:
						{
							SeanceNum	=	( int ) CCommon.IsNull( Command.GetScalar(" exec dbo.pMega_OpenGate_Days;4  @TaskCode='ErcGate',@ParamCode='NumSeance' ") , (int) 0 );
							ErcConfig	= new	CSalaryConfig();
							ErcConfig.Open( ErcDate );
							InputDir	=	(string)ErcConfig["SalaryDir"]	+ "\\IN\\" ;
							break;
        	        }
        	        case	WorkModes.OPENGATE	:
						{
							SeanceNum	=	( int ) CCommon.IsNull( Command.GetScalar(" exec dbo.pMega_OpenGate_Days;4  @TaskCode='OpenGate',@ParamCode='NumSeance' ") , (int) 0 );
							ErcConfig	= new	COpenwayConfig();
							ErcConfig.Open( ErcDate );
							InputDir	=	(string)ErcConfig["InputDir"]	;
							break;
						}
					case	WorkModes.SAKR	:
					case	WorkModes.ERC	:
						{
							SeanceNum	=	( int ) CCommon.IsNull( Command.GetScalar(" exec dbo.pMega_OpenGate_Days;4  @TaskCode='ErcGate',@ParamCode='NumSeance' ") , (int) 0 );
							ErcConfig	= new	CErcConfig();
							ErcConfig.Open( ErcDate );
							InputDir	=	(string)ErcConfig["InputDir"]	;
							break;
						}
					default:
						{
							CCommon.Print( "","Ошибка в строке параметров программы ! ");
							return;
							break;
						}
		}
		if	( ! ErcConfig.IsValid() ) {
			CCommon.Print( "  Ошибка чтения настроек программы из " + ErcConfig.Config_FileName() );
			System.Console.WriteLine(ErcConfig.ErrInfo())		;
			return;
		}
		if	( SeanceNum < 1 ) {
			CCommon.Print( " Ошибка определения номера сеанса " );
			return	;
		}
		TodayDir	=	(string)ErcConfig.TodayDir()		;
		TmpDir		=	(string)ErcConfig.TmpDir()		;
		StatDir		=	(string)ErcConfig.StatDir()		;
		if ( (TodayDir == null) || (InputDir == null) ) {
			CCommon.Print( "  Ошибка чтения настроек программы из " + ErcConfig.Config_FileName() );
			return;
		}
		TodayDir	=	TodayDir.Trim() ;
		InputDir	=	InputDir.Trim();
		if	( (TodayDir == "")  || (InputDir == "" )  ) {
			CCommon.Print( "  Ошибка чтения настроек программы из " + ErcConfig.Config_FileName()  );
			return;
		}
		if	( ! CCommon.DirExists(StatDir) )
			CCommon.MkDir(StatDir);
		if	( ! CCommon.SaveText( StatDir + "\\" + "test.dat" , "test.dat" , CAbc.CHARSET_DOS ) ) {
			CCommon.Print( " Ошибка записи в каталог " + StatDir );
			return	;
		}
		CCommon.DeleteFile(StatDir + "\\" + "test.dat");
		LogFileName	=	ErcConfig.LogDir() + "\\"
				+	(	( WorkMode == WorkModes.CHECK )
					?	"W" + CCommon.Hour(CCommon.Now()).ToString("00") + CCommon.Minute(CCommon.Now()).ToString("00") + CCommon.Second(CCommon.Now()).ToString("00")
					: 	"SEANS"+SeanceNum.ToString("000")
					)
				+	".TXT";
		CCommon.Print("  Беру настройки шлюза здесь :  " + ErcConfig.Config_FileName() );
		// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
		// основной блог работы программы
		switch	( WorkMode ) {
			// - - - - - - - - - - - - - - - - - - - - - - -
			// Загружаем сторонние платежи
			case	WorkModes.SAKR	:
			{
				foreach	( string FName in CCommon.GetFileList( InputDir+"!A??_*.*" ) )
						if	( FName != null )
							if	( FileName.Trim() != "")
								ProcessAFile( Command , FileName , false );
				// Загружаем допреквизиты
				foreach	( string FName in CCommon.GetFileList( InputDir+"!E*.*" ) )
					if	( FName != null )
						if	( FName.Trim() != "")
							if	( ErcEFile.Load( Command , FileName ) )
								CCommon.MoveFile( FileName , TodayDir + CAbc.SLASH + CCommon.GetFileName(FileName) );
				// Завершающие действия
				CCommon.Print( "  Пересчет промежуточных данных ...");
				Command.Execute("exec dbo.pMega_OpenGate_PalvisBind ");
				break;
			}
			// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
			// Загружаем коммунальные платежи из файла, который выбирает пользователь
			case	WorkModes.ERC	:
			{
				FileName	=	SelectFileName( InputDir + "!A*.*" ) ;
				while	( FileName!="" ) {
					if( ErcAFile.UserAccepted ( FileName ) )
						ProcessAFile( Command , FileName , true )	;
					FileName	=	SelectFileName( InputDir + "!A*.*" )	;
				}
				break;
			}
			// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
			// Загружаем зарплатные платежи из файла, который выбирает пользователь
			case	WorkModes.SALARY	:
			{
				FileName	=	SelectFileName( InputDir + "!A*.Z*" ) ;
				while	( FileName != "" ) {
					if	( ErcAFile.UserAccepted ( FileName ) )
						ProcessAFile( Command , FileName , true )	;
					FileName	=	SelectFileName( InputDir + "!A*.*" )	;
				}
				break;
			}
			// - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -
			// Загружаем платежи по универсальному шлюзу из файла, который выбирает пользователь
			case	WorkModes.OPENGATE	:
			{
				if	( FileName	!=	"" ) {
					if	( CCommon.FileExists( FileName ) )
						if	( ErcAFile.UserAccepted ( FileName ) )
							ProcessAFile( Command , FileName , true )	;
				}
				else {
					FileName	=	SelectFileNameGUI( SettingsDir, InputDir ) ;
					if	( FileName!="" )
						if	( ErcAFile.UserAccepted ( FileName ) ) {
							ProcessAFile( Command , FileName , true )	;
							if	( CCommon.FileExists( FileName ) )
								CCommon.DeleteFile( FileName ) ;
						}
				}
				break;
			}
			// - - - - - - - - - - - -
			// Только проверка файла
			case	WorkModes.CHECK	:
			{
				string	AboutError	=	"" ;
				if	( FileName.Length>0 )
					if	( ! CCommon.FileExists( FileName ) ) {
							CCommon.Print("","Не найден файл " + FileName);
							FileName	=	"";
					}
				if	( FileName != "" ) {
						Err.LogToConsole()	;
                        AboutError	=	ErcAFile.Check( Command , FileName )	;
                        CConsole.Clear();
						if	( AboutError	==	"" )
							CCommon.Print(""," "+ErcAFile.String_Count.ToString()+" строк на общую суммму "
								+CCommon.StrN(ErcAFile.Total_Credit,11).Replace(",","."),""
                                                                ," Ошибок не найдено.");
						else {
							CCommon.AppendText( LogFileName , CCommon.Now() + "   "+ CCommon.Upper(CCommon.GetUserName()) + "  проверяет файл "
										+ CCommon.GetFileName(FileName) + CAbc.CRLF + CAbc.CRLF + AboutError + CAbc.CRLF , CAbc.CHARSET_WINDOWS );
                                        	CCommon.Print( AboutError );
						}
				}
				else {
					FileName	=	SelectFileNameGUI( SettingsDir, InputDir ) ;
					if	( FileName!="" ) {
						if	( ErcAFile.UserAccepted ( FileName ) ) {
								AboutError	=	ErcAFile.Check( Command , FileName )	;
								CConsole.Clear();
								if	( AboutError	==	"" )
									CCommon.Print(""," "+ErcAFile.String_Count.ToString()+" строк на общую суммму "
										+CCommon.StrN(ErcAFile.Total_Credit,11).Replace(",","."),""
		                                                               ," Ошибок не найдено.");
							else {
								CCommon.AppendText( LogFileName , CCommon.Now() + "   "+ CCommon.Upper(CCommon.GetUserName()) + "  проверяет файл "
												+ CCommon.GetFileName(FileName) + CAbc.CRLF + CAbc.CRLF + AboutError + CAbc.CRLF , CAbc.CHARSET_WINDOWS );
													CCommon.Print( AboutError );
							}
						}
						if	( CCommon.FileExists( FileName ) )
							CCommon.DeleteFile( FileName ) ;
					}
				}
				break;
			}
			// - - - - - - - - - - - - - - - - - -
			// `запретить` заготовки из А-файла
			case	WorkModes.DISABLE	:
			{
				CConsole.Clear();
				Err.LogToConsole()	;
				if	( CCommon.ParamCount() > 2 )
					FileName	=	CAbc.ParamStr[2].Trim();
				if	( FileName	==	"" )
					CCommon.Print("","Не задано имя файла !");
				else {
					FileName	=	CCommon.GetFileName ( FileName );
					if	( Command.Execute( "exec dbo.pMega_OpenGate_Disable @TaskCode='OpenGate',@FileName='" + FileName + "'" ) )
						CCommon.Print("","заготовки из файла  " + FileName + "  запрещены .");
					else
						CCommon.Print("","Ошибка выполнения команды на сервере !");
				}
				break;
			}
		}
		Command.Close();
		Connection.Close();
	}
}