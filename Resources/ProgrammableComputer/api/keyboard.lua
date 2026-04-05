-- Keyboard mapping API for programmable computers.
-- Exposes:
--   K (key code table)
--   keycode_to_text(code, shift, layout)
--   keyboard_on(event_name, handler)
--   keyboard_off(event_name, handler)
--   keyboard_emit(event_name, ...)
--   keyboard_on_key(key_code, handler, include_repeat)
--   keyboard_off_key(binding)

K = {
  A=10,B=11,C=12,D=13,E=14,F=15,G=16,H=17,I=18,J=19,K=20,L=21,M=22,
  N=23,O=24,P=25,Q=26,R=27,S=28,T=29,U=30,V=31,W=32,X=33,Y=34,Z=35,
  D0=36,D1=37,D2=38,D3=39,D4=40,D5=41,D6=42,D7=43,D8=44,D9=45,
  ESCAPE=56,CONTROL=57,SHIFT=58,ALT=59,LSYSTEM=60,RSYSTEM=61,
  LBRACKET=63,RBRACKET=64,SEMICOLON=65,COMMA=66,PERIOD=67,
  APOSTROPHE=68,SLASH=69,BACKSLASH=70,TILDE=71,EQUAL=72,
  SPACE=73,RETURN=74,NUMPADENTER=75,BACKSPACE=76,TAB=77,
  PAGEUP=78,PAGEDOWN=79,END=80,HOME=81,INSERT=82,DELETE=83,
  MINUS=84,LEFT=90,RIGHT=91,UP=92,DOWN=93,
  F1=94,F2=95,F3=96,F4=97,F5=98,F6=99,F7=100,F8=101,F9=102,
  F10=103,F11=104,F12=105,
}

local _keyboard_listeners = {
    key = {},
    text = {},
}

local function _listener_bucket(event_name)
    local bucket = _keyboard_listeners[event_name]
    if bucket == nil then
        bucket = {}
        _keyboard_listeners[event_name] = bucket
    end

    return bucket
end

function keyboard_on(event_name, handler)
    if type(event_name) ~= 'string' or type(handler) ~= 'function' then
        return false
    end

    local bucket = _listener_bucket(event_name)
    bucket[#bucket + 1] = handler
    return true
end

function keyboard_off(event_name, handler)
    if type(event_name) ~= 'string' or type(handler) ~= 'function' then
        return false
    end

    local bucket = _keyboard_listeners[event_name]
    if bucket == nil then
        return false
    end

    for i = #bucket, 1, -1 do
        if bucket[i] == handler then
            table.remove(bucket, i)
            return true
        end
    end

    return false
end

function keyboard_emit(event_name, ...)
    local bucket = _keyboard_listeners[event_name]
    if bucket == nil or #bucket == 0 then
        return
    end

    for i = 1, #bucket do
        if type(bucket[i]) == 'function' then
            if type(pcall) == 'function' then
                local ok, err = pcall(bucket[i], ...)
                if not ok then
                    term.writeLine('keyboard listener error [' .. tostring(event_name) .. ']: ' .. tostring(err))
                end
            else
                bucket[i](...)
            end
        end
    end
end

-- Binds one callback for a specific key code.
-- include_repeat=false: callback only on first press
-- include_repeat=true: callback on first press and repeat
function keyboard_on_key(key_code, handler, include_repeat)
    if type(key_code) ~= 'number' or type(handler) ~= 'function' then
        return nil
    end

    local binding = {
        key_code = key_code,
        include_repeat = include_repeat == true,
    }

    binding.on_pressed = function(code, ctrl, alt, shift, meta, layout)
        if code == binding.key_code then
            handler(code, false, ctrl, alt, shift, meta, layout)
        end
    end

    binding.on_repeat = function(code, ctrl, alt, shift, meta, layout)
        if binding.include_repeat and code == binding.key_code then
            handler(code, true, ctrl, alt, shift, meta, layout)
        end
    end

    keyboard_on('key_pressed', binding.on_pressed)
    keyboard_on('key_repeat', binding.on_repeat)

    return binding
end

function keyboard_off_key(binding)
    if type(binding) ~= 'table' then
        return false
    end

    local removed_pressed = false
    local removed_repeat = false

    if type(binding.on_pressed) == 'function' then
        removed_pressed = keyboard_off('key_pressed', binding.on_pressed)
    end

    if type(binding.on_repeat) == 'function' then
        removed_repeat = keyboard_off('key_repeat', binding.on_repeat)
    end

    return removed_pressed or removed_repeat
end

local function map_letter(code, shift, layout)
    local letter = nil

    if layout == 1 then -- qwertz
        if code == K.Y then
            letter = 'z'
        elseif code == K.Z then
            letter = 'y'
        end
    elseif layout == 2 then -- azerty
        if code == K.Q then
            letter = 'a'
        elseif code == K.A then
            letter = 'q'
        elseif code == K.W then
            letter = 'z'
        elseif code == K.Z then
            letter = 'w'
        elseif code == K.SEMICOLON then
            letter = 'm'
        end
    end

    if letter == nil and code >= K.A and code <= K.Z then
        letter = string.char(string.byte('a') + (code - K.A))
    end

    if letter == nil then
        return nil
    end

    return shift and string.upper(letter) or letter
end

local function map_symbol(code, shift, layout)
    if layout == 1 then -- qwertz
        local normal = {
            [K.D0] = '0', [K.D1] = '1', [K.D2] = '2', [K.D3] = '3', [K.D4] = '4',
            [K.D5] = '5', [K.D6] = '6', [K.D7] = '7', [K.D8] = '8', [K.D9] = '9',
            [K.TILDE] = '^', [K.RBRACKET] = '+', [K.BACKSLASH] = '#',
            [K.COMMA] = ',', [K.PERIOD] = '.', [K.SLASH] = '-', [K.SPACE] = ' '
        }
        local shifted = {
            [K.D0] = '=', [K.D1] = '!', [K.D2] = '"', [K.D4] = '$', [K.D5] = '%',
            [K.D6] = '&', [K.D7] = '/', [K.D8] = '(', [K.D9] = ')',
            [K.MINUS] = '?', [K.EQUAL] = '`', [K.LBRACKET] = '*', [K.RBRACKET] = "'",
            [K.COMMA] = ';', [K.PERIOD] = ':', [K.SLASH] = '_'
        }
        return shift and shifted[code] or normal[code]
    elseif layout == 2 then -- azerty
        local normal = {
            [K.D1] = '&', [K.D3] = '"', [K.D4] = "'", [K.D5] = '(', [K.D6] = '-',
            [K.D8] = '_', [K.MINUS] = ')', [K.EQUAL] = '=', [K.LBRACKET] = '$',
            [K.RBRACKET] = '*', [K.COMMA] = ';', [K.PERIOD] = ':', [K.SLASH] = '!',
            [K.SPACE] = ' '
        }
        local shifted = {
            [K.D0] = '0', [K.D1] = '1', [K.D2] = '2', [K.D3] = '3', [K.D4] = '4',
            [K.D5] = '5', [K.D6] = '6', [K.D7] = '7', [K.D8] = '8', [K.D9] = '9',
            [K.EQUAL] = '+', [K.LBRACKET] = '^', [K.APOSTROPHE] = '%',
            [K.COMMA] = '?', [K.PERIOD] = '.', [K.SLASH] = '/'
        }
        return shift and shifted[code] or normal[code]
    else -- us (layout == 0)
        local normal = {
            [K.D0] = '0', [K.D1] = '1', [K.D2] = '2', [K.D3] = '3', [K.D4] = '4',
            [K.D5] = '5', [K.D6] = '6', [K.D7] = '7', [K.D8] = '8', [K.D9] = '9',
            [K.TILDE] = '`', [K.MINUS] = '-', [K.EQUAL] = '=', [K.LBRACKET] = '[',
            [K.RBRACKET] = ']', [K.BACKSLASH] = '\\', [K.SEMICOLON] = ';',
            [K.APOSTROPHE] = "'", [K.COMMA] = ',', [K.PERIOD] = '.', [K.SLASH] = '/',
            [K.SPACE] = ' '
        }
        local shifted = {
            [K.D0] = ')', [K.D1] = '!', [K.D2] = '@', [K.D3] = '#', [K.D4] = '$',
            [K.D5] = '%', [K.D6] = '^', [K.D7] = '&', [K.D8] = '*', [K.D9] = '(',
            [K.TILDE] = '~', [K.MINUS] = '_', [K.EQUAL] = '+', [K.LBRACKET] = '{',
            [K.RBRACKET] = '}', [K.BACKSLASH] = '|', [K.SEMICOLON] = ':',
            [K.APOSTROPHE] = '"', [K.COMMA] = '<', [K.PERIOD] = '>', [K.SLASH] = '?'
        }
        return shift and shifted[code] or normal[code]
    end
end

function keycode_to_text(code, shift, layout)
    local letter = map_letter(code, shift, layout)
    if letter ~= nil then
        return letter
    end

    return map_symbol(code, shift, layout)
end
