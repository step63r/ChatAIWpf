#pragma once

#include <windows.h>
#include <PathCch.h>
#include <Shlwapi.h>
#include <string.h>

#include <array>
#include <codecvt>
#include <iostream>
#include <vector>
#include <fstream>
#include <msclr/marshal.h>
#include <msclr/marshal_cppstd.h>

#include <voicevox_core.h>

using namespace msclr::interop;
using namespace System;
using namespace System::Text;
using namespace System::Runtime::InteropServices;

namespace ChatAIWpf {
	namespace Clr {
		/// <summary>
		/// VoiceVoxネイティブクラス
		/// </summary>
		public class MyVoiceVox {
		public:
			MyVoiceVox(std::wstring wsDict);				// コンストラクタ
			virtual ~MyVoiceVox();							// デストラクタ

			HRESULT Initialize();							// coreの初期化
			HRESULT GenerateVoice(std::wstring wsWords);	// 音声の生成

		private:
			std::wstring m_wsDict;							// OpenJTalk辞書ファイルパス
			uint8_t* m_output_wav;							// 

			std::string GetOpenJTalkDict();					// OpenJTalk辞書のパスを取得
			std::wstring GetWaveFileName();					// 音声ファイル名を取得
			std::wstring GetExePath();						// 自分自身のあるパスを取得する
			std::wstring GetExeDirectory();					// 自分自身のあるディレクトリを取得する
			std::string wide_to_utf8_cppapi(std::wstring const& src);	// ワイド文字列をUTF8に変換する
			std::wstring utf8_to_wide_cppapi(std::string const& src);	// UTF8をワイド文字列に変換する
		};

		/// <summary>
		/// VoiceVoxをCLRで使うためのラッパークラス
		/// </summary>
		public ref class VoiceVoxWrapper
		{
		public:
			VoiceVoxWrapper(String^ dict);					// コンストラクタ
			virtual ~VoiceVoxWrapper();						// デストラクタ

			int Initialize();								// coreの初期化
			int GenerateVoice(String^ words);				// 音声の生成

		private:
			MyVoiceVox* m_lpVoiceVox;						// VoiceVoxインスタンス
		};
	}
}
