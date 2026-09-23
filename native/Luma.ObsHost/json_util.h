#pragma once

#include <cstdint>
#include <string>

inline void SkipWs(const std::string& json, size_t& pos)
{
    while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t' || json[pos] == '\r' || json[pos] == '\n'))
    {
        pos++;
    }
}

inline bool FindKey(const std::string& json, const char* key, size_t& valuePos)
{
    const std::string pat = std::string("\"") + key + "\"";
    size_t i = 0;
    while (true)
    {
        i = json.find(pat, i);
        if (i == std::string::npos)
        {
            return false;
        }
        size_t after = i + pat.size();
        SkipWs(json, after);
        if (after < json.size() && json[after] == ':')
        {
            after++;
            SkipWs(json, after);
            valuePos = after;
            return true;
        }
        i = after;
    }
}

inline std::string JsonUnescape(const std::string& in)
{
    std::string out;
    out.reserve(in.size());
    for (size_t i = 0; i < in.size();)
    {
        if (in[i] != '\\' || i + 1 >= in.size())
        {
            out += in[i++];
            continue;
        }
        const char n = in[++i];
        i++;
        switch (n)
        {
        case 'n':
            out += '\n';
            break;
        case 'r':
            out += '\r';
            break;
        case 't':
            out += '\t';
            break;
        case 'u':
            if (i + 4 <= in.size())
            {
                unsigned code = 0;
                for (int k = 0; k < 4; k++)
                {
                    char h = in[i + k];
                    code <<= 4;
                    if (h >= '0' && h <= '9')
                        code += static_cast<unsigned>(h - '0');
                    else if (h >= 'a' && h <= 'f')
                        code += static_cast<unsigned>(h - 'a' + 10);
                    else if (h >= 'A' && h <= 'F')
                        code += static_cast<unsigned>(h - 'A' + 10);
                }
                i += 4;
                if (code < 0x80)
                {
                    out += static_cast<char>(code);
                }
                else if (code < 0x800)
                {
                    out += static_cast<char>(0xC0 | (code >> 6));
                    out += static_cast<char>(0x80 | (code & 0x3F));
                }
                else
                {
                    out += static_cast<char>(0xE0 | (code >> 12));
                    out += static_cast<char>(0x80 | ((code >> 6) & 0x3F));
                    out += static_cast<char>(0x80 | (code & 0x3F));
                }
            }
            break;
        default:
            out += n;
            break;
        }
    }
    return out;
}

inline std::string JsonString(const std::string& json, const char* key, const std::string& fallback = {})
{
    size_t pos = 0;
    if (!FindKey(json, key, pos) || pos >= json.size() || json[pos] != '"')
    {
        return fallback;
    }
    pos++;
    std::string raw;
    while (pos < json.size())
    {
        char c = json[pos++];
        if (c == '"')
        {
            break;
        }
        raw += c;
        if (c == '\\' && pos < json.size())
        {
            raw += json[pos++];
        }
    }
    return JsonUnescape(raw);
}

inline int64_t JsonInt(const std::string& json, const char* key, int64_t fallback = 0)
{
    size_t pos = 0;
    if (!FindKey(json, key, pos))
    {
        return fallback;
    }
    try
    {
        return static_cast<int64_t>(std::stoll(json.substr(pos)));
    }
    catch (...)
    {
        return fallback;
    }
}

inline double JsonDouble(const std::string& json, const char* key, double fallback = 0)
{
    size_t pos = 0;
    if (!FindKey(json, key, pos))
    {
        return fallback;
    }
    try
    {
        return std::stod(json.substr(pos));
    }
    catch (...)
    {
        return fallback;
    }
}

inline bool JsonBool(const std::string& json, const char* key, bool fallback = false)
{
    size_t pos = 0;
    if (!FindKey(json, key, pos))
    {
        return fallback;
    }
    if (json.compare(pos, 4, "true") == 0)
    {
        return true;
    }
    if (json.compare(pos, 5, "false") == 0)
    {
        return false;
    }
    return fallback;
}

inline std::string JsonEscape(const std::string& in)
{
    std::string out;
    out.reserve(in.size() + 8);
    for (unsigned char c : in)
    {
        switch (c)
        {
        case '\\':
            out += "\\\\";
            break;
        case '"':
            out += "\\\"";
            break;
        case '\n':
            out += "\\n";
            break;
        case '\r':
            out += "\\r";
            break;
        case '\t':
            out += "\\t";
            break;
        default:
            out += static_cast<char>(c);
            break;
        }
    }
    return out;
}
