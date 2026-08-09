// File Converter modern Windows 11 context menu.
// License: http://www.gnu.org/licenses/gpl.html GPL version 3.

#include <windows.h>
#include <shellapi.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <strsafe.h>
#include <xmllite.h>

#include <algorithm>
#include <cstdio>
#include <cwctype>
#include <mutex>
#include <new>
#include <string>
#include <utility>
#include <vector>

#include "resource.h"

#pragma comment(lib, "Ole32.lib")
#pragma comment(lib, "Shell32.lib")
#pragma comment(lib, "Shlwapi.lib")
#pragma comment(lib, "XmlLite.lib")

namespace
{
    // This value must stay identical to the CLSID in Packaging/ModernMenu/AppxManifest.xml.in.
    const CLSID CLSID_FileConverterModernMenu =
        { 0x4f57c104, 0x510d, 0x4a96, { 0x9b, 0x86, 0x77, 0x07, 0x2b, 0x35, 0x25, 0x47 } };

    HINSTANCE moduleHandle = nullptr;
    volatile long objectCount = 0;
    volatile long serverLockCount = 0;

    enum class CommandKind
    {
        Root,
        Preset,
        Settings,
    };

    struct CommandDefinition
    {
        CommandKind Kind = CommandKind::Preset;
        std::wstring FullName;
        std::wstring DisplayName;
        std::wstring OutputType;
        std::vector<std::wstring> InputTypes;
    };

    std::wstring ToLower(std::wstring value)
    {
        std::transform(value.begin(), value.end(), value.begin(), [](wchar_t character) { return static_cast<wchar_t>(std::towlower(character)); });
        return value;
    }

    std::wstring GetModulePath()
    {
        wchar_t path[MAX_PATH] = {};
        DWORD length = GetModuleFileNameW(moduleHandle, path, ARRAYSIZE(path));
        return length > 0 && length < ARRAYSIZE(path) ? std::wstring(path, length) : std::wstring();
    }

    std::wstring GetDirectoryName(const std::wstring& path)
    {
        std::wstring::size_type separator = path.find_last_of(L"\\/");
        return separator == std::wstring::npos ? std::wstring() : path.substr(0, separator);
    }

    bool FileExists(const std::wstring& path)
    {
        DWORD attributes = GetFileAttributesW(path.c_str());
        return attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
    }

    std::wstring ReadRegistryString(HKEY root, const wchar_t* subKey, const wchar_t* valueName)
    {
        wchar_t value[32768] = {};
        DWORD size = sizeof(value);
        DWORD type = 0;
        if (RegGetValueW(root, subKey, valueName, RRF_RT_REG_SZ, &type, value, &size) != ERROR_SUCCESS)
        {
            return std::wstring();
        }

        return value;
    }

    std::wstring GetApplicationPath()
    {
        std::wstring registryPath = ReadRegistryString(HKEY_CURRENT_USER, L"Software\\FileConverter", L"Path");
        if (FileExists(registryPath))
        {
            return registryPath;
        }

        registryPath = ReadRegistryString(HKEY_LOCAL_MACHINE, L"Software\\FileConverter", L"Path");
        if (FileExists(registryPath))
        {
            return registryPath;
        }

        std::wstring fallback = GetDirectoryName(GetModulePath()) + L"\\FileConverter.exe";
        return FileExists(fallback) ? fallback : std::wstring();
    }

    std::wstring GetSettingsPath()
    {
        wchar_t localAppData[MAX_PATH] = {};
        if (SUCCEEDED(SHGetFolderPathW(nullptr, CSIDL_LOCAL_APPDATA, nullptr, SHGFP_TYPE_CURRENT, localAppData)))
        {
            std::wstring userSettings = std::wstring(localAppData) + L"\\FileConverter\\Settings.user.xml";
            if (FileExists(userSettings))
            {
                return userSettings;
            }
        }

        std::wstring applicationPath = GetApplicationPath();
        std::wstring defaultSettings = GetDirectoryName(applicationPath) + L"\\Settings.default.xml";
        if (FileExists(defaultSettings))
        {
            return defaultSettings;
        }

        defaultSettings = GetDirectoryName(GetModulePath()) + L"\\Settings.default.xml";
        return FileExists(defaultSettings) ? defaultSettings : std::wstring();
    }

    bool IsName(IXmlReader* reader, const wchar_t* expected)
    {
        const wchar_t* name = nullptr;
        UINT length = 0;
        return SUCCEEDED(reader->GetLocalName(&name, &length)) && name != nullptr && _wcsicmp(name, expected) == 0;
    }

    std::wstring ReadAttribute(IXmlReader* reader, const wchar_t* name)
    {
        if (FAILED(reader->MoveToAttributeByName(name, nullptr)))
        {
            return std::wstring();
        }

        const wchar_t* value = nullptr;
        UINT length = 0;
        std::wstring result;
        if (SUCCEEDED(reader->GetValue(&value, &length)) && value != nullptr)
        {
            result.assign(value, length);
        }

        reader->MoveToElement();
        return result;
    }

    std::vector<CommandDefinition> LoadPresets()
    {
        std::vector<CommandDefinition> presets;
        std::wstring settingsPath = GetSettingsPath();
        if (settingsPath.empty())
        {
            return presets;
        }

        IStream* stream = nullptr;
        if (FAILED(SHCreateStreamOnFileEx(settingsPath.c_str(), STGM_READ | STGM_SHARE_DENY_WRITE, FILE_ATTRIBUTE_NORMAL, FALSE, nullptr, &stream)))
        {
            return presets;
        }

        IXmlReader* reader = nullptr;
        HRESULT result = CreateXmlReader(__uuidof(IXmlReader), reinterpret_cast<void**>(&reader), nullptr);
        if (SUCCEEDED(result))
        {
            result = reader->SetInput(stream);
        }

        CommandDefinition current;
        bool inPreset = false;
        XmlNodeType nodeType = XmlNodeType_None;
        while (SUCCEEDED(result) && (result = reader->Read(&nodeType)) == S_OK)
        {
            if (nodeType == XmlNodeType_Element && IsName(reader, L"ConversionPreset"))
            {
                current = CommandDefinition();
                current.FullName = ReadAttribute(reader, L"Name");
                current.OutputType = ReadAttribute(reader, L"OutputType");
                current.DisplayName = current.FullName;
                std::wstring::size_type separator = 0;
                while ((separator = current.DisplayName.find(L'/', separator)) != std::wstring::npos)
                {
                    current.DisplayName.replace(separator, 1, L" › ");
                    separator += 3;
                }

                inPreset = !current.FullName.empty();
            }
            else if (nodeType == XmlNodeType_Element && inPreset && IsName(reader, L"InputTypes"))
            {
                XmlNodeType valueType = XmlNodeType_None;
                if (reader->Read(&valueType) == S_OK && (valueType == XmlNodeType_Text || valueType == XmlNodeType_CDATA || valueType == XmlNodeType_Whitespace))
                {
                    const wchar_t* value = nullptr;
                    UINT length = 0;
                    if (SUCCEEDED(reader->GetValue(&value, &length)) && value != nullptr && length > 0)
                    {
                        current.InputTypes.push_back(ToLower(std::wstring(value, length)));
                    }
                }
            }
            else if (nodeType == XmlNodeType_EndElement && inPreset && IsName(reader, L"ConversionPreset"))
            {
                if (!current.InputTypes.empty())
                {
                    presets.push_back(current);
                }

                inPreset = false;
            }
        }

        if (reader != nullptr)
        {
            reader->Release();
        }

        stream->Release();
        return presets;
    }

    std::vector<std::wstring> GetSelectedPaths(IShellItemArray* items)
    {
        std::vector<std::wstring> paths;
        if (items == nullptr)
        {
            return paths;
        }

        DWORD count = 0;
        if (FAILED(items->GetCount(&count)))
        {
            return paths;
        }

        for (DWORD index = 0; index < count; ++index)
        {
            IShellItem* item = nullptr;
            if (FAILED(items->GetItemAt(index, &item)))
            {
                continue;
            }

            wchar_t* path = nullptr;
            if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &path)) && path != nullptr)
            {
                if ((GetFileAttributesW(path) & FILE_ATTRIBUTE_DIRECTORY) == 0)
                {
                    paths.emplace_back(path);
                }

                CoTaskMemFree(path);
            }

            item->Release();
        }

        return paths;
    }

    std::wstring GetExtension(const std::wstring& path)
    {
        const wchar_t* extension = PathFindExtensionW(path.c_str());
        if (extension == nullptr || *extension == L'\0')
        {
            return std::wstring();
        }

        return ToLower(extension[0] == L'.' ? extension + 1 : extension);
    }

    bool IsCompatible(const CommandDefinition& preset, const std::vector<std::wstring>& paths)
    {
        if (paths.empty())
        {
            return false;
        }

        std::vector<std::wstring> extensions;
        for (const std::wstring& path : paths)
        {
            std::wstring extension = GetExtension(path);
            if (extension.empty() || std::find(preset.InputTypes.begin(), preset.InputTypes.end(), extension) == preset.InputTypes.end())
            {
                return false;
            }

            if (std::find(extensions.begin(), extensions.end(), extension) == extensions.end())
            {
                extensions.push_back(extension);
            }
        }

        if (_wcsicmp(preset.OutputType.c_str(), L"PdfMerge") == 0)
        {
            return paths.size() >= 2 && extensions.size() == 1 && extensions[0] == L"pdf";
        }

        return true;
    }

    std::wstring QuoteArgument(const std::wstring& argument)
    {
        std::wstring quoted = L"\"";
        size_t backslashes = 0;
        for (wchar_t character : argument)
        {
            if (character == L'\\')
            {
                ++backslashes;
                continue;
            }

            if (character == L'\"')
            {
                quoted.append((backslashes * 2) + 1, L'\\');
                quoted.push_back(L'\"');
            }
            else
            {
                quoted.append(backslashes, L'\\');
                quoted.push_back(character);
            }

            backslashes = 0;
        }

        quoted.append(backslashes * 2, L'\\');
        quoted.push_back(L'\"');
        return quoted;
    }

    bool WriteUtf8InputList(const std::vector<std::wstring>& paths, std::wstring& filePath)
    {
        wchar_t tempDirectory[MAX_PATH] = {};
        wchar_t tempFile[MAX_PATH] = {};
        if (GetTempPathW(ARRAYSIZE(tempDirectory), tempDirectory) == 0 || GetTempFileNameW(tempDirectory, L"FCM", 0, tempFile) == 0)
        {
            return false;
        }

        HANDLE file = CreateFileW(tempFile, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_TEMPORARY, nullptr);
        if (file == INVALID_HANDLE_VALUE)
        {
            DeleteFileW(tempFile);
            return false;
        }

        const BYTE bom[] = { 0xEF, 0xBB, 0xBF };
        DWORD written = 0;
        bool succeeded = WriteFile(file, bom, sizeof(bom), &written, nullptr) != FALSE;
        for (const std::wstring& path : paths)
        {
            int size = WideCharToMultiByte(CP_UTF8, 0, path.c_str(), static_cast<int>(path.size()), nullptr, 0, nullptr, nullptr);
            std::string utf8(static_cast<size_t>(size), '\0');
            if (size > 0)
            {
                WideCharToMultiByte(CP_UTF8, 0, path.c_str(), static_cast<int>(path.size()), &utf8[0], size, nullptr, nullptr);
            }

            utf8.append("\r\n");
            succeeded = succeeded && WriteFile(file, utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr) != FALSE;
        }

        CloseHandle(file);
        if (!succeeded)
        {
            DeleteFileW(tempFile);
            return false;
        }

        filePath = tempFile;
        return true;
    }

    HRESULT LaunchApplication(const CommandDefinition& command, const std::vector<std::wstring>& paths)
    {
        std::wstring applicationPath = GetApplicationPath();
        if (applicationPath.empty())
        {
            return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
        }

        std::wstring arguments;
        if (command.Kind == CommandKind::Settings)
        {
            arguments = L"--settings";
        }
        else
        {
            arguments = L"--conversion-preset " + QuoteArgument(command.FullName);
            for (const std::wstring& path : paths)
            {
                arguments += L" " + QuoteArgument(path);
            }

            if (arguments.size() > 30000)
            {
                std::wstring inputList;
                if (!WriteUtf8InputList(paths, inputList))
                {
                    return HRESULT_FROM_WIN32(GetLastError());
                }

                arguments = L"--conversion-preset " + QuoteArgument(command.FullName) + L" --input-files " + QuoteArgument(inputList) + L" --delete-input-list";
            }
        }

        std::wstring workingDirectory = GetDirectoryName(applicationPath);
        SHELLEXECUTEINFOW executeInfo = {};
        executeInfo.cbSize = sizeof(executeInfo);
        executeInfo.fMask = SEE_MASK_FLAG_NO_UI;
        executeInfo.lpFile = applicationPath.c_str();
        executeInfo.lpParameters = arguments.c_str();
        executeInfo.lpDirectory = workingDirectory.c_str();
        executeInfo.nShow = SW_SHOWNORMAL;
        if (!ShellExecuteExW(&executeInfo))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        return S_OK;
    }

    class ExplorerCommand;

    class ExplorerCommandEnumerator final : public IEnumExplorerCommand
    {
    public:
        explicit ExplorerCommandEnumerator(std::vector<CommandDefinition> commands);

        IFACEMETHODIMP QueryInterface(REFIID interfaceId, void** result) override;
        IFACEMETHODIMP_(ULONG) AddRef() override;
        IFACEMETHODIMP_(ULONG) Release() override;
        IFACEMETHODIMP Next(ULONG count, IExplorerCommand** commands, ULONG* fetched) override;
        IFACEMETHODIMP Skip(ULONG count) override;
        IFACEMETHODIMP Reset() override;
        IFACEMETHODIMP Clone(IEnumExplorerCommand** result) override;

    private:
        ~ExplorerCommandEnumerator();

        volatile long referenceCount = 1;
        std::vector<CommandDefinition> commands;
        ULONG index = 0;
    };

    class ExplorerCommand final : public IExplorerCommand
    {
    public:
        ExplorerCommand()
        {
            this->definition.Kind = CommandKind::Root;
            this->definition.DisplayName = L"File Converter";
            InterlockedIncrement(&objectCount);
        }

        explicit ExplorerCommand(const CommandDefinition& definition)
            : definition(definition)
        {
            InterlockedIncrement(&objectCount);
        }

        IFACEMETHODIMP QueryInterface(REFIID interfaceId, void** result) override
        {
            if (result == nullptr)
            {
                return E_POINTER;
            }

            *result = nullptr;
            if (interfaceId == IID_IUnknown || interfaceId == __uuidof(IExplorerCommand))
            {
                *result = static_cast<IExplorerCommand*>(this);
                this->AddRef();
                return S_OK;
            }

            return E_NOINTERFACE;
        }

        IFACEMETHODIMP_(ULONG) AddRef() override
        {
            return static_cast<ULONG>(InterlockedIncrement(&this->referenceCount));
        }

        IFACEMETHODIMP_(ULONG) Release() override
        {
            ULONG count = static_cast<ULONG>(InterlockedDecrement(&this->referenceCount));
            if (count == 0)
            {
                delete this;
            }

            return count;
        }

        IFACEMETHODIMP GetTitle(IShellItemArray*, wchar_t** title) override
        {
            return title == nullptr ? E_POINTER : SHStrDupW(this->definition.DisplayName.c_str(), title);
        }

        IFACEMETHODIMP GetIcon(IShellItemArray*, wchar_t** icon) override
        {
            if (icon == nullptr)
            {
                return E_POINTER;
            }

            if (this->definition.Kind == CommandKind::Root)
            {
                std::wstring iconPath = GetModulePath() + L",-" + std::to_wstring(IDI_APPICON);
                return SHStrDupW(iconPath.c_str(), icon);
            }

            *icon = nullptr;
            return E_NOTIMPL;
        }

        IFACEMETHODIMP GetToolTip(IShellItemArray*, wchar_t** tooltip) override
        {
            if (tooltip == nullptr)
            {
                return E_POINTER;
            }

            *tooltip = nullptr;
            return E_NOTIMPL;
        }

        IFACEMETHODIMP GetCanonicalName(GUID* canonicalName) override
        {
            if (canonicalName == nullptr)
            {
                return E_POINTER;
            }

            *canonicalName = this->definition.Kind == CommandKind::Root ? CLSID_FileConverterModernMenu : GUID_NULL;
            return S_OK;
        }

        IFACEMETHODIMP GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override
        {
            if (state == nullptr)
            {
                return E_POINTER;
            }

            std::vector<std::wstring> paths = GetSelectedPaths(items);
            if (this->definition.Kind == CommandKind::Root)
            {
                std::vector<CommandDefinition> compatible;
                for (const CommandDefinition& preset : LoadPresets())
                {
                    if (IsCompatible(preset, paths))
                    {
                        compatible.push_back(preset);
                    }
                }

                {
                    std::lock_guard<std::mutex> guard(this->syncRoot);
                    this->subCommands = compatible;
                }

                *state = compatible.empty() ? ECS_HIDDEN : ECS_ENABLED;
            }
            else if (this->definition.Kind == CommandKind::Settings)
            {
                *state = GetApplicationPath().empty() ? ECS_DISABLED : ECS_ENABLED;
            }
            else
            {
                *state = IsCompatible(this->definition, paths) ? ECS_ENABLED : ECS_DISABLED;
            }

            return S_OK;
        }

        IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
        {
            if (this->definition.Kind == CommandKind::Root)
            {
                return E_NOTIMPL;
            }

            return LaunchApplication(this->definition, GetSelectedPaths(items));
        }

        IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
        {
            if (flags == nullptr)
            {
                return E_POINTER;
            }

            *flags = this->definition.Kind == CommandKind::Root ? ECF_HASSUBCOMMANDS : ECF_DEFAULT;
            return S_OK;
        }

        IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
        {
            if (commands == nullptr)
            {
                return E_POINTER;
            }

            *commands = nullptr;
            if (this->definition.Kind != CommandKind::Root)
            {
                return E_NOTIMPL;
            }

            std::vector<CommandDefinition> snapshot;
            {
                std::lock_guard<std::mutex> guard(this->syncRoot);
                snapshot = this->subCommands;
            }

            CommandDefinition settings;
            settings.Kind = CommandKind::Settings;
            settings.DisplayName = L"Configure presets...";
            snapshot.push_back(settings);

            *commands = new (std::nothrow) ExplorerCommandEnumerator(snapshot);
            return *commands == nullptr ? E_OUTOFMEMORY : S_OK;
        }

    private:
        ~ExplorerCommand()
        {
            InterlockedDecrement(&objectCount);
        }

        volatile long referenceCount = 1;
        CommandDefinition definition;
        std::mutex syncRoot;
        std::vector<CommandDefinition> subCommands;
    };

    ExplorerCommandEnumerator::ExplorerCommandEnumerator(std::vector<CommandDefinition> commands)
        : commands(std::move(commands))
    {
        InterlockedIncrement(&objectCount);
    }

    ExplorerCommandEnumerator::~ExplorerCommandEnumerator()
    {
        InterlockedDecrement(&objectCount);
    }

    IFACEMETHODIMP ExplorerCommandEnumerator::QueryInterface(REFIID interfaceId, void** result)
    {
        if (result == nullptr)
        {
            return E_POINTER;
        }

        *result = nullptr;
        if (interfaceId == IID_IUnknown || interfaceId == __uuidof(IEnumExplorerCommand))
        {
            *result = static_cast<IEnumExplorerCommand*>(this);
            this->AddRef();
            return S_OK;
        }

        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) ExplorerCommandEnumerator::AddRef()
    {
        return static_cast<ULONG>(InterlockedIncrement(&this->referenceCount));
    }

    IFACEMETHODIMP_(ULONG) ExplorerCommandEnumerator::Release()
    {
        ULONG count = static_cast<ULONG>(InterlockedDecrement(&this->referenceCount));
        if (count == 0)
        {
            delete this;
        }

        return count;
    }

    IFACEMETHODIMP ExplorerCommandEnumerator::Next(ULONG count, IExplorerCommand** output, ULONG* fetched)
    {
        if (output == nullptr || (count != 1 && fetched == nullptr))
        {
            return E_POINTER;
        }

        ULONG produced = 0;
        while (produced < count && this->index < this->commands.size())
        {
            output[produced] = new (std::nothrow) ExplorerCommand(this->commands[this->index]);
            if (output[produced] == nullptr)
            {
                break;
            }

            ++produced;
            ++this->index;
        }

        if (fetched != nullptr)
        {
            *fetched = produced;
        }

        return produced == count ? S_OK : S_FALSE;
    }

    IFACEMETHODIMP ExplorerCommandEnumerator::Skip(ULONG count)
    {
        ULONG remaining = static_cast<ULONG>(this->commands.size()) - this->index;
        ULONG skipped = (std::min)(count, remaining);
        this->index += skipped;
        return skipped == count ? S_OK : S_FALSE;
    }

    IFACEMETHODIMP ExplorerCommandEnumerator::Reset()
    {
        this->index = 0;
        return S_OK;
    }

    IFACEMETHODIMP ExplorerCommandEnumerator::Clone(IEnumExplorerCommand** result)
    {
        if (result == nullptr)
        {
            return E_POINTER;
        }

        ExplorerCommandEnumerator* clone = new (std::nothrow) ExplorerCommandEnumerator(this->commands);
        if (clone == nullptr)
        {
            *result = nullptr;
            return E_OUTOFMEMORY;
        }

        clone->index = this->index;
        *result = clone;
        return S_OK;
    }

    class ClassFactory final : public IClassFactory
    {
    public:
        ClassFactory()
        {
            InterlockedIncrement(&objectCount);
        }

        IFACEMETHODIMP QueryInterface(REFIID interfaceId, void** result) override
        {
            if (result == nullptr)
            {
                return E_POINTER;
            }

            *result = nullptr;
            if (interfaceId == IID_IUnknown || interfaceId == IID_IClassFactory)
            {
                *result = static_cast<IClassFactory*>(this);
                this->AddRef();
                return S_OK;
            }

            return E_NOINTERFACE;
        }

        IFACEMETHODIMP_(ULONG) AddRef() override
        {
            return static_cast<ULONG>(InterlockedIncrement(&this->referenceCount));
        }

        IFACEMETHODIMP_(ULONG) Release() override
        {
            ULONG count = static_cast<ULONG>(InterlockedDecrement(&this->referenceCount));
            if (count == 0)
            {
                delete this;
            }

            return count;
        }

        IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID interfaceId, void** result) override
        {
            if (outer != nullptr)
            {
                return CLASS_E_NOAGGREGATION;
            }

            ExplorerCommand* command = new (std::nothrow) ExplorerCommand();
            if (command == nullptr)
            {
                return E_OUTOFMEMORY;
            }

            HRESULT queryResult = command->QueryInterface(interfaceId, result);
            command->Release();
            return queryResult;
        }

        IFACEMETHODIMP LockServer(BOOL lock) override
        {
            if (lock)
            {
                InterlockedIncrement(&serverLockCount);
            }
            else
            {
                InterlockedDecrement(&serverLockCount);
            }

            return S_OK;
        }

    private:
        ~ClassFactory()
        {
            InterlockedDecrement(&objectCount);
        }

        volatile long referenceCount = 1;
    };
}

BOOL APIENTRY DllMain(HINSTANCE instance, DWORD reason, void*)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        moduleHandle = instance;
        DisableThreadLibraryCalls(instance);
    }

    return TRUE;
}

extern "C" HRESULT __stdcall DllCanUnloadNow()
{
    return objectCount == 0 && serverLockCount == 0 ? S_OK : S_FALSE;
}

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID classId, REFIID interfaceId, void** result)
{
    if (result == nullptr)
    {
        return E_POINTER;
    }

    *result = nullptr;
    if (classId != CLSID_FileConverterModernMenu)
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    ClassFactory* factory = new (std::nothrow) ClassFactory();
    if (factory == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    HRESULT queryResult = factory->QueryInterface(interfaceId, result);
    factory->Release();
    return queryResult;
}
