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
local editor = nil
local prompt_text
local tier_info = computer.tier()
local gpu_tier = tonumber(tier_info.gpuTier) or 0

local function color_enabled()
    return gpu_tier >= 1
end

local function maybe_set_text_color(color)
    if color_enabled() then
        term.setTextColor(color)
    end
end

local function maybe_reset_colors()
    if color_enabled() then
        term.resetColors()
    end
end

local function resolve_editor_path(raw)
    if raw:sub(1, 1) == '/' then
        return canonical_path(raw)
    end

    return join_path(cwd, raw)
end

local function split_text_lines(text)
    text = tostring(text or ''):gsub('\r\n', '\n')
    if text == '' then
        return { '' }
    end

    local lines = {}
    local start = 1
    while true do
        local idx = text:find('\n', start, true)
        if idx == nil then
            table.insert(lines, text:sub(start))
            break
        end

        table.insert(lines, text:sub(start, idx - 1))
        start = idx + 1
        if start > #text + 1 then
            table.insert(lines, '')
            break
        end
    end

    if #lines == 0 then
        lines[1] = ''
    end

    return lines
end

local function join_text_lines(lines)
    return table.concat(lines or { '' }, '\n')
end

local function editor_gutter_width(line_count, width)
    if width < 14 then
        return 0
    end

    local digits = math.max(2, #tostring(math.max(1, line_count or 1)))
    return digits + 2
end

local function editor_view_rows()
    local _, height = term.getSize()
    local footer_rows = 2
    return math.max(1, height - 1 - footer_rows)
end

local function editor_text_width(line_count)
    local width = select(1, term.getSize())
    local gutter = editor_gutter_width(line_count, width)
    return math.max(1, width - gutter)
end

local function begin_save_as_prompt()
    if editor == nil then
        return
    end

    editor.prompt = {
        label = 'Save as: ',
        text = editor.path,
        cursor = #editor.path + 1,
    }
    editor.status = 'Enter a target path and press Enter.'
end

local function commit_save_as_prompt()
    if editor == nil or editor.prompt == nil then
        return false
    end

    local raw = editor.prompt.text or ''
    if raw == '' then
        editor.prompt = nil
        editor.status = 'Save-as cancelled: empty path.'
        return false
    end

    local target = resolve_editor_path(raw)
    local ok = fs.write(target, join_text_lines(editor.lines))
    editor.prompt = nil

    if ok then
        editor.path = target
        editor.dirty = false
        editor.quit_armed = false
        editor.status = 'Saved as ' .. target
        return true
    end

    editor.status = 'Save-as failed: ' .. target
    return false
end

local function cancel_save_as_prompt()
    if editor == nil or editor.prompt == nil then
        return
    end

    editor.prompt = nil
    editor.status = 'Save-as cancelled.'
end

local function ensure_editor_visible()
    if editor == nil then
        return
    end

    local text_width = editor_text_width(#editor.lines)
    local content_rows = editor_view_rows()

    if editor.cursor_y < editor.scroll_y then
        editor.scroll_y = editor.cursor_y
    elseif editor.cursor_y >= editor.scroll_y + content_rows then
        editor.scroll_y = editor.cursor_y - content_rows + 1
    end

    if editor.cursor_x < editor.scroll_x then
        editor.scroll_x = editor.cursor_x
    elseif editor.cursor_x >= editor.scroll_x + text_width then
        editor.scroll_x = editor.cursor_x - text_width + 1
    end
end

local function draw_editor()
    if editor == nil then
        return
    end

    ensure_editor_visible()

    local width, height = term.getSize()
    local footer_rows = gpu_tier >= 3 and 2 or 1
    local content_rows = editor_view_rows()
    local gutter_width = editor_gutter_width(#editor.lines, width)
    local text_width = math.max(1, width - gutter_width)
    local title = 'EDIT ' .. editor.path
    if editor.dirty then
        title = title .. ' *'
    end

    term.clear()
    term.setCursorPos(1, 1)
    term.clearLine()
    maybe_set_text_color(term.colors.cyan)
    term.write(title:sub(1, width))
    maybe_reset_colors()

    for row = 1, content_rows do
        local line_idx = editor.scroll_y + row - 1
        local text = editor.lines[line_idx] or ''
        term.setCursorPos(1, row + 1)
        term.clearLine()
        if gutter_width > 0 then
            local line_label = tostring(line_idx)
            if #line_label < gutter_width - 1 then
                line_label = string.rep(' ', (gutter_width - 1) - #line_label) .. line_label
            end

            maybe_set_text_color(term.colors.gray)
            term.write(line_label .. '|')
            maybe_reset_colors()
        end

        if text ~= '' then
            term.write(text:sub(editor.scroll_x, editor.scroll_x + text_width - 1))
        end
    end

    local status = editor.status or ''
    if status == '' then
        status = 'Ctrl+S save  Ctrl+Q quit  Ln ' .. tostring(editor.cursor_y) .. ', Col ' .. tostring(editor.cursor_x)
    end

    term.setCursorPos(1, height - 1)
    term.clearLine()
    if editor.dirty then
        maybe_set_text_color(term.colors.yellow)
    else
        maybe_set_text_color(term.colors.lime)
    end
    term.write(status:sub(1, width))
    maybe_reset_colors()

    term.setCursorPos(1, height)
    term.clearLine()
    if editor.prompt ~= nil then
        local prompt = editor.prompt
        local prompt_line = prompt.label .. prompt.text
        term.write(prompt_line:sub(1, width))
        term.setCursorPos(math.min(width, #prompt.label + prompt.cursor), height)
    else
        local help_line = 'Ctrl+S save  Ctrl+Shift+S save-as  Ctrl+Q quit  PgUp/PgDn page'
        maybe_set_text_color(term.colors.silver)
        term.write(help_line:sub(1, width))
        maybe_reset_colors()
        term.setCursorPos(gutter_width + (editor.cursor_x - editor.scroll_x + 1), editor.cursor_y - editor.scroll_y + 2)
    end

    term.setCursorBlink(true)
end

local function open_editor(path)
    local target = resolve_editor_path(path)
    local content = fs.read(target)
    editor = {
        path = target,
        lines = split_text_lines(content),
        cursor_x = 1,
        cursor_y = 1,
        scroll_x = 1,
        scroll_y = 1,
        preferred_x = 1,
        dirty = content == nil,
        quit_armed = false,
        status = content == nil and ('New file: ' .. target) or ('Opened ' .. target),
        prompt = nil,
    }
    mode = 'editor'
    draw_editor()
end

local function close_editor(force)
    if editor == nil then
        return
    end

    if editor.dirty and not force then
        editor.quit_armed = true
        editor.status = 'Unsaved changes. Ctrl+Q again to discard or Ctrl+S to save.'
        draw_editor()
        return
    end

    editor = nil
    draw_shell()
end

local function save_editor()
    if editor == nil then
        return false
    end

    local ok = fs.write(editor.path, join_text_lines(editor.lines))
    if ok then
        editor.dirty = false
        editor.quit_armed = false
        editor.status = 'Saved ' .. editor.path
    else
        editor.status = 'Save failed: ' .. editor.path
    end

    draw_editor()
    return ok
end

local function editor_current_line()
    return editor.lines[editor.cursor_y] or ''
end

local function editor_set_current_line(text)
    editor.lines[editor.cursor_y] = text
end

local function editor_mark_dirty(status)
    editor.dirty = true
    editor.quit_armed = false
    editor.status = status or ''
end

local function editor_insert_text(text)
    local line_text = editor_current_line()
    local before = line_text:sub(1, editor.cursor_x - 1)
    local after = line_text:sub(editor.cursor_x)
    editor_set_current_line(before .. text .. after)
    editor.cursor_x = editor.cursor_x + #text
    editor.preferred_x = editor.cursor_x
    editor_mark_dirty()
end

local function handle_editor_key_input(code, is_repeat, ctrl, alt, shift, meta, layout)
    if editor == nil then
        return
    end

    if editor.prompt ~= nil then
        local prompt = editor.prompt

        if code == K.ESCAPE and not is_repeat then
            cancel_save_as_prompt()
            draw_editor()
            return
        end

        if (code == K.RETURN or code == K.NUMPADENTER) and not is_repeat then
            commit_save_as_prompt()
            draw_editor()
            return
        end

        if ctrl or alt or meta then
            return
        end

        if code == K.LEFT then
            prompt.cursor = math.max(1, prompt.cursor - 1)
        elseif code == K.RIGHT then
            prompt.cursor = math.min(#prompt.text + 1, prompt.cursor + 1)
        elseif code == K.HOME then
            prompt.cursor = 1
        elseif code == K.END then
            prompt.cursor = #prompt.text + 1
        elseif code == K.BACKSPACE then
            if prompt.cursor > 1 then
                prompt.text = prompt.text:sub(1, prompt.cursor - 2) .. prompt.text:sub(prompt.cursor)
                prompt.cursor = prompt.cursor - 1
            end
        elseif code == K.DELETE then
            if prompt.cursor <= #prompt.text then
                prompt.text = prompt.text:sub(1, prompt.cursor - 1) .. prompt.text:sub(prompt.cursor + 1)
            end
        elseif not is_repeat then
            local typed = keycode_to_text(code, shift, layout)
            if typed ~= nil and typed ~= '' then
                prompt.text = prompt.text:sub(1, prompt.cursor - 1) .. typed .. prompt.text:sub(prompt.cursor)
                prompt.cursor = prompt.cursor + #typed
            end
        end

        draw_editor()
        return
    end

    if ctrl and shift and code == K.S and not is_repeat then
        begin_save_as_prompt()
        draw_editor()
        return
    end

    if ctrl and code == K.S and not is_repeat then
        save_editor()
        return
    end

    if ctrl and code == K.Q and not is_repeat then
        close_editor(editor.quit_armed)
        return
    end

    if ctrl or alt or meta then
        return
    end

    local line_text = editor_current_line()

    if code == K.RETURN or code == K.NUMPADENTER then
        local before = line_text:sub(1, editor.cursor_x - 1)
        local after = line_text:sub(editor.cursor_x)
        editor.lines[editor.cursor_y] = before
        table.insert(editor.lines, editor.cursor_y + 1, after)
        editor.cursor_y = editor.cursor_y + 1
        editor.cursor_x = 1
        editor.preferred_x = 1
        editor_mark_dirty()
    elseif code == K.BACKSPACE then
        if editor.cursor_x > 1 then
            editor_set_current_line(line_text:sub(1, editor.cursor_x - 2) .. line_text:sub(editor.cursor_x))
            editor.cursor_x = editor.cursor_x - 1
            editor.preferred_x = editor.cursor_x
            editor_mark_dirty()
        elseif editor.cursor_y > 1 then
            local prev = editor.lines[editor.cursor_y - 1] or ''
            editor.cursor_x = #prev + 1
            editor.lines[editor.cursor_y - 1] = prev .. line_text
            table.remove(editor.lines, editor.cursor_y)
            editor.cursor_y = editor.cursor_y - 1
            editor.preferred_x = editor.cursor_x
            editor_mark_dirty()
        end
    elseif code == K.DELETE then
        if editor.cursor_x <= #line_text then
            editor_set_current_line(line_text:sub(1, editor.cursor_x - 1) .. line_text:sub(editor.cursor_x + 1))
            editor_mark_dirty()
        elseif editor.cursor_y < #editor.lines then
            editor.lines[editor.cursor_y] = line_text .. (editor.lines[editor.cursor_y + 1] or '')
            table.remove(editor.lines, editor.cursor_y + 1)
            editor_mark_dirty()
        end
    elseif code == K.LEFT then
        if editor.cursor_x > 1 then
            editor.cursor_x = editor.cursor_x - 1
        elseif editor.cursor_y > 1 then
            editor.cursor_y = editor.cursor_y - 1
            editor.cursor_x = #(editor.lines[editor.cursor_y] or '') + 1
        end
        editor.preferred_x = editor.cursor_x
        editor.status = ''
    elseif code == K.RIGHT then
        if editor.cursor_x <= #line_text then
            editor.cursor_x = editor.cursor_x + 1
        elseif editor.cursor_y < #editor.lines then
            editor.cursor_y = editor.cursor_y + 1
            editor.cursor_x = 1
        end
        editor.preferred_x = editor.cursor_x
        editor.status = ''
    elseif code == K.UP then
        if editor.cursor_y > 1 then
            editor.cursor_y = editor.cursor_y - 1
            local target = editor.lines[editor.cursor_y] or ''
            editor.cursor_x = math.min(#target + 1, editor.preferred_x)
        end
        editor.status = ''
    elseif code == K.DOWN then
        if editor.cursor_y < #editor.lines then
            editor.cursor_y = editor.cursor_y + 1
            local target = editor.lines[editor.cursor_y] or ''
            editor.cursor_x = math.min(#target + 1, editor.preferred_x)
        end
        editor.status = ''
    elseif code == K.PAGEUP then
        local step = editor_view_rows()
        editor.cursor_y = math.max(1, editor.cursor_y - step)
        local target = editor.lines[editor.cursor_y] or ''
        editor.cursor_x = math.min(#target + 1, editor.preferred_x)
        editor.status = ''
    elseif code == K.PAGEDOWN then
        local step = editor_view_rows()
        editor.cursor_y = math.min(#editor.lines, editor.cursor_y + step)
        local target = editor.lines[editor.cursor_y] or ''
        editor.cursor_x = math.min(#target + 1, editor.preferred_x)
        editor.status = ''
    elseif code == K.HOME then
        editor.cursor_x = 1
        editor.preferred_x = 1
        editor.status = ''
    elseif code == K.END then
        editor.cursor_x = #line_text + 1
        editor.preferred_x = editor.cursor_x
        editor.status = ''
    elseif code == K.TAB then
        editor_insert_text('  ')
    elseif not is_repeat then
        local typed = keycode_to_text(code, shift, layout)
        if typed ~= nil and typed ~= '' then
            editor_insert_text(typed)
        end
    end

    if editor ~= nil then
        draw_editor()
    end
end

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

    local chunk, err = load(source, '@' .. tostring(path))
    if chunk == nil then
        term.writeLine(tostring(err))
        return false
    end

    local result = chunk()
    if result ~= nil then
        term.writeLine('=> ' .. tostring(result))
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
    term.write(prompt_text())
end

function prompt_text()
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
    return resolve_editor_path(raw)
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
        term.writeLine('edit <path>       - open VM text editor')
        term.writeLine('  editor keys     - Ctrl+S save, Ctrl+Shift+S save-as')
        term.writeLine('                    Ctrl+Q quit, PgUp/PgDn page scroll')
        term.writeLine('                    GPU T1+: color accents (no GPU: monochrome)')
        term.writeLine('run <path>        - execute Lua file')
        term.writeLine('. <path>          - source Lua file into current shell')
        term.writeLine('source <path>     - same as .')
        term.writeLine('echo <text...>    - print text ($PWD/$HOME supported)')
        term.writeLine('history           - show command history')
        term.writeLine('!!                - run previous command')
        term.writeLine('termdebug         - run terminal debug demo')
        term.writeLine('pumptest          - enable all linked pumps')
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
    elseif cmd == 'pumptest' then
        run_file(resolve_program_path('pumptest.lua', cwd))
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
                local chunk, err = load(source, '@' .. tostring(resolved))
                if chunk == nil then
                    term.writeLine(tostring(err))
                else
                    local result = chunk()
                    if result ~= nil then
                        term.writeLine('=> ' .. tostring(result))
                    end
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
    elseif cmd == 'edit' then
        local raw = args[2]
        if raw == nil then
            term.writeLine('Usage: edit <path>')
            return
        end

        open_editor(raw)
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
    if mode == 'editor' then
        handle_editor_key_input(code, is_repeat, ctrl, alt, shift, meta, layout)
        return
    end

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
    elseif ev == 'run_command' then
        local command = tostring(a or '')
        if command ~= '' and mode ~= 'editor' then
            execute_shell_command(command)
            if mode == 'shell' then
                local x2, y2 = term.getCursorPos()
                if x2 ~= 1 then
                    term.setCursorPos(1, y2 + 1)
                end

                line = ''
                cursor = 1
                reset_history_navigation()
                write_prompt()
            end
        end
    elseif ev == 'touch' then
        local x = a
        local y = b
        local button = c or 1
        touch_emit('touch', x, y, button)
        touch_emit('touch_pressed', x, y, button)
    end
end