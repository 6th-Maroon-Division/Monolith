-- Lua Computer default boot script
-- API modules are loaded first so boot logic can stay small.
local function load_api(path)
    local source = fs.read(path)
    if source == nil then
        term.writeLine('API file not found: ' .. path)
        return false
    end

    local exec = computer.exec(source, false)
    if not exec.ok then
        term.writeLine('API load error [' .. path .. ']: ' .. tostring(exec.error))
        return false
    end

    return true
end

load_api('/api/keyboard.lua')
load_api('/api/path.lua')
load_api('/api/touch.lua')

local W, H = term.getSize()
local line = ''
local cursor = 1
local mode = 'shell'
local cwd = '/'
local input_row = 1
local shell_history = {}
local repl_history = {}
local history_index = nil
local history_stash = ''

local function eval_source(source)
    local exec = computer.exec(source, true)
    if not exec.ok then
        term.writeLine(tostring(exec.error))
    elseif exec.hasResult then
        term.writeLine('=> ' .. tostring(exec.result))
    end
end

local function run_file(path)
    if path == nil then
        term.writeLine('Program not found.')
        return false
    end

    local source = fs.read(path)
    if source == nil then
        term.writeLine('File not found: ' .. path)
        return false
    end

    local exec = computer.exec(source, false)
    if not exec.ok then
        term.writeLine(tostring(exec.error))
        return false
    end

    if exec.hasResult then
        term.writeLine('=> ' .. tostring(exec.result))
    end

    return true
end

local function load_compat()
    local compat_path = '/lib/cc_compat.lua'
    if not fs.exists(compat_path) then
        return
    end

    local source = fs.read(compat_path)
    if source == nil then
        term.writeLine('Failed to read compat library: ' .. compat_path)
        return
    end

    local exec = computer.exec(source, false)
    if not exec.ok then
        term.writeLine('Compat load error: ' .. tostring(exec.error))
    end
end

local function write_prompt()
    local _, y = term.getCursorPos()
    input_row = y
    if mode == 'repl' then
        term.write('lua> ')
    else
        term.write('> ')
    end
end

local function prompt_text()
    return mode == 'repl' and 'lua> ' or (cwd .. ' $ ')
end

local function active_history()
    return mode == 'repl' and repl_history or shell_history
end

local function reset_history_navigation()
    history_index = nil
    history_stash = ''
end

local function add_history_entry(entry)
    if entry == nil or entry == '' then
        return
    end

    local history = active_history()
    if history[#history] ~= entry then
        table.insert(history, entry)
    end

    if #history > 100 then
        table.remove(history, 1)
    end
end

local function render_input_line()
    local prompt = prompt_text()
    term.setCursorPos(1, input_row)
    term.clearLine()
    term.write(prompt .. line)
    term.setCursorPos(#prompt + cursor, input_row)
end

local function set_line(new_line)
    line = new_line or ''
    cursor = #line + 1
    render_input_line()
end

local function navigate_history(delta)
    local history = active_history()
    if #history == 0 then
        return
    end

    if history_index == nil then
        if delta < 0 then
            history_stash = line
            history_index = #history
        else
            return
        end
    else
        history_index = history_index + delta
        if history_index < 1 then
            history_index = 1
        elseif history_index > #history then
            history_index = nil
            set_line(history_stash)
            history_stash = ''
            return
        end
    end

    set_line(history[history_index] or '')
end

local function draw_shell()
    mode = 'shell'
    line = ''
    cursor = 1
    reset_history_navigation()
    term.clear()
    term.setCursorPos(1, 1)
    term.write('Lua Computer v1.0  [' .. W .. 'x' .. H .. ']')
    term.setCursorPos(1, 2)
    term.write('Edit /boot/init.lua to customise this script.')
    term.setCursorPos(1, 4)
    write_prompt()
end

local function draw_repl()
    mode = 'repl'
    line = ''
    cursor = 1
    reset_history_navigation()
    term.clear()
    term.setCursorPos(1, 1)
    term.write('Lua REPL')
    term.setCursorPos(1, 2)
    term.write("Type 'exit' to return to shell.")
    term.setCursorPos(1, 4)
    write_prompt()
end

local function split_shell_args(command)
    local args = {}
    local current = ''
    local quote = nil
    local escaped = false

    for i = 1, #command do
        local ch = command:sub(i, i)

        if escaped then
            current = current .. ch
            escaped = false
        elseif ch == '\\' and quote ~= "'" then
            escaped = true
        elseif quote ~= nil then
            if ch == quote then
                quote = nil
            else
                current = current .. ch
            end
        elseif ch == '"' or ch == "'" then
            quote = ch
        elseif ch == ' ' or ch == '\t' then
            if #current > 0 then
                table.insert(args, current)
                current = ''
            end
        else
            current = current .. ch
        end
    end

    if escaped then
        current = current .. '\\'
    end

    if #current > 0 then
        table.insert(args, current)
    end

    return args
end

local function expand_shell_vars(text)
    local expanded = text
    expanded = expanded:gsub('%$PWD', cwd)
    expanded = expanded:gsub('%$HOME', '/')
    return expanded
end

local function resolve_file_argument(raw)
    if raw:sub(1, 1) == '/' then
        return canonical_path(raw)
    end

    return join_path(cwd, raw)
end

local function expand_history_input(input)
    if input == '!!' then
        if #shell_history == 0 then
            term.writeLine('history: no previous command')
            return nil
        end

        local previous = shell_history[#shell_history]
        term.writeLine(previous)
        return previous
    end

    return input
end

local function execute_shell_command(input)
    local args = split_shell_args(input)
    if #args == 0 then
        return
    end

    local cmd = args[1]
    if cmd == 'help' then
        term.writeLine('help              - show this help')
        term.writeLine('cls|clear         - clear the screen')
        term.writeLine('cd [path]         - change directory (default /)')
        term.writeLine('pwd               - print current directory')
        term.writeLine('ls [path]         - list directory')
        term.writeLine('cat <path>        - print file')
        term.writeLine('run <path>        - execute Lua file')
        term.writeLine('. <path>          - source Lua file into current shell')
        term.writeLine('source <path>     - same as .')
        term.writeLine('echo <text...>    - print text ($PWD/$HOME supported)')
        term.writeLine('history           - show command history')
        term.writeLine('!!                - run previous command')
        term.writeLine('termdebug         - run terminal debug demo')
        term.writeLine('lua               - open Lua REPL terminal')
        term.writeLine('lua <expr>        - evaluate Lua expression')
    elseif cmd == 'cls' or cmd == 'clear' then
        line = ''
        cursor = 1
        reset_history_navigation()
        term.clear()
        term.setCursorPos(1, 1)
        write_prompt()
    elseif cmd == 'pwd' then
        term.writeLine(cwd)
    elseif cmd == 'cd' then
        local targetInput = args[2] or '/'
        local target = resolve_directory_path(targetInput, cwd)
        if is_directory(target) then
            cwd = target
        else
            term.writeLine('Directory not found: ' .. target)
        end
    elseif cmd == 'termdebug' then
        run_file(resolve_program_path('termdebug.lua', cwd))
    elseif cmd == 'run' then
        local path = args[2]
        if path == nil then
            term.writeLine('Usage: run <path>')
            return
        end

        local resolved = resolve_program_path(path, cwd)
        if resolved == nil then
            term.writeLine('Program not found: ' .. path)
        else
            run_file(resolved)
        end
    elseif cmd == '.' or cmd == 'source' then
        local path = args[2]
        if path == nil then
            term.writeLine('Usage: ' .. cmd .. ' <path>')
            return
        end

        local resolved = resolve_program_path(path, cwd)
        if resolved == nil then
            term.writeLine('Program not found: ' .. path)
        else
            local source = fs.read(resolved)
            if source == nil then
                term.writeLine('File not found: ' .. resolved)
            else
                local exec = computer.exec(source, false)
                if not exec.ok then
                    term.writeLine(tostring(exec.error))
                elseif exec.hasResult then
                    term.writeLine('=> ' .. tostring(exec.result))
                end
            end
        end
    elseif cmd == 'ls' then
        local path = args[2] and resolve_directory_path(args[2], cwd) or cwd
        local entries = fs.list(path)
        for i = 1, #entries do
            term.writeLine(entries[i])
        end
    elseif cmd == 'cat' then
        local raw = args[2]
        if raw == nil then
            term.writeLine('Usage: cat <path>')
            return
        end

        local path = resolve_file_argument(raw)
        local content = fs.read(path)
        if content == nil then
            term.writeLine('File not found: ' .. path)
        else
            term.writeLine(content)
        end
    elseif cmd == 'echo' then
        local chunks = {}
        for i = 2, #args do
            chunks[#chunks + 1] = expand_shell_vars(args[i])
        end
        term.writeLine(table.concat(chunks, ' '))
    elseif cmd == 'history' then
        for i = 1, #shell_history do
            term.writeLine(tostring(i) .. '  ' .. shell_history[i])
        end
    elseif cmd == 'lua' and #args == 1 then
        draw_repl()
    elseif cmd == 'lua' and #args > 1 then
        local source = input:sub(5)
        eval_source(source)
    else
        eval_source(input)
    end
end

load_compat()
draw_shell()

local function handle_key_input(code, is_repeat, ctrl, alt, shift, meta, layout)
    if (code == K.RETURN or code == K.NUMPADENTER) and not is_repeat then
        local suppress_prompt = false
        local input = line
        local x, y = term.getCursorPos()
        term.setCursorPos(1, y + 1)

        if mode == 'repl' then
            if input == 'exit' then
                add_history_entry(input)
                draw_shell()
                suppress_prompt = true
            elseif input ~= '' then
                add_history_entry(input)
                eval_source(input)
                local x2, y2 = term.getCursorPos()
                if x2 ~= 1 then
                    term.setCursorPos(1, y2 + 1)
                end
            end
        else
            if input ~= '' then
                local expanded = expand_history_input(input)
                if expanded == nil then
                    suppress_prompt = false
                else
                    add_history_entry(expanded)
                    execute_shell_command(expanded)

                    local x2, y2 = term.getCursorPos()
                    if not suppress_prompt and x2 ~= 1 then
                        term.setCursorPos(1, y2 + 1)
                    end
                end
            end
        end

        if not suppress_prompt then
            line = ''
            cursor = 1
            reset_history_navigation()
            write_prompt()
        end
    elseif code == K.BACKSPACE then
        if cursor > 1 and #line > 0 then
            line = line:sub(1, cursor - 2) .. line:sub(cursor)
            cursor = cursor - 1
            reset_history_navigation()
            render_input_line()
        end
    elseif code == K.LEFT then
        if cursor > 1 then
            cursor = cursor - 1
            render_input_line()
        end
    elseif code == K.RIGHT then
        if cursor <= #line then
            cursor = cursor + 1
            render_input_line()
        end
    elseif code == K.HOME then
        cursor = 1
        render_input_line()
    elseif code == K.END then
        cursor = #line + 1
        render_input_line()
    elseif code == K.DELETE then
        if cursor <= #line then
            line = line:sub(1, cursor - 1) .. line:sub(cursor + 1)
            reset_history_navigation()
            render_input_line()
        end
    elseif code == K.UP then
        navigate_history(-1)
    elseif code == K.DOWN then
        navigate_history(1)
    elseif not ctrl and not alt and not meta then
        local typed = keycode_to_text(code, shift, layout)
        if typed ~= nil and typed ~= '' then
            keyboard_emit('text', typed, code, is_repeat, ctrl, alt, shift, meta, layout)
            line = line:sub(1, cursor - 1) .. typed .. line:sub(cursor)
            cursor = cursor + #typed
            reset_history_navigation()
            render_input_line()
        end
    end
end

keyboard_on('key_pressed', function(code, ctrl, alt, shift, meta, layout)
    handle_key_input(code, false, ctrl, alt, shift, meta, layout)
end)

keyboard_on('key_repeat', function(code, ctrl, alt, shift, meta, layout)
    handle_key_input(code, true, ctrl, alt, shift, meta, layout)
end)

keyboard_on('key_released', function(code, ctrl, alt, shift, meta, layout)
    -- Reserved hook for default boot behavior on release.
end)

while true do
    local ev, a, b, c, d, e, f, g = event.pull()
    if ev == 'key' then
        local code = a
        local held = b
        local ctrl = c
        local alt = d
        local shift = e
        local meta = f
        local layout = g
        layout = layout or 0
        keyboard_emit('key', code, held, ctrl, alt, shift, meta, layout)
        if held then
            keyboard_emit('key_repeat', code, ctrl, alt, shift, meta, layout)
        else
            keyboard_emit('key_pressed', code, ctrl, alt, shift, meta, layout)
        end
    elseif ev == 'key_up' then
        local code = a
        local ctrl = c
        local alt = d
        local shift = e
        local meta = f
        local layout = g
        layout = layout or 0
        keyboard_emit('key_up', code, ctrl, alt, shift, meta, layout)
        keyboard_emit('key_released', code, ctrl, alt, shift, meta, layout)
    elseif ev == 'touch' then
        local x = a
        local y = b
        local button = c or 1
        touch_emit('touch', x, y, button)
        touch_emit('touch_pressed', x, y, button)
    end
end