jQuery(document).ready(function ($) {
    var emptyGuid = "00000000000000000000000000000000";

    self.baseUrl = (function () {
        var pathname = window.location.pathname;
        if (pathname[pathname.length - 1] !== "/") {
            pathname += "/";
        }

        return window.location.protocol + "//" + window.location.host + pathname + "api/";
    })();

    self.invokeEndpoint = self.baseUrl + "invoke";
    self.completionEndpoint = self.baseUrl + "complete/";

    function admin() {
        var nextEventId = 0;
        var self = this;
        self.to = "calculator/" + emptyGuid;
        self.shellFn = {};

        self.createMethodCall = function (method, args) {
            return {
                to: self.to,
                id: ++nextEventId,
                method: method,
                args: args
            };
        };

        self.guid = (function () {
            function s4() {
                return Math.floor((1 + Math.random()) * 0x10000)
                           .toString(16)
                           .substring(1);
            }
            return function () {
                return s4() + s4() + s4() + s4() + s4() + s4() + s4() + s4();
            };
        })();

        self.req = function (method, value) {

            return $.ajax({
                method: "POST",
                url: self.invokeEndpoint,
                data: JSON.stringify(self.createMethodCall(method, value)),
                contentType: "application/json"
            }).then(function (result) {
                return result;
            }, function (error) {
                return error.responseJSON || error;
            });
        };

        self.shellFn.req = function (cmd) {
            var method = cmd.args[0];
            var args = cmd.args.slice(1);
            self.reqJs(self.req(method, args));
        };

        self.reqJs = function (request) {
            var startTime = performance.now();
            var printedResult = false;
            request.then(
                function (next) {
                    printedResult = true;
                    window.terminal.echo(JSON.stringify(next, null, "  "));
                    window.terminal.echo((performance.now() - startTime).toFixed(2) + "ms");
                    window.terminal.resume();
                },
                function (error) {
                    printedResult = true;
                    var msg = error;
                    if (typeof error === "object") {
                        msg = JSON.stringify(error, null, "  ");
                    }
                    window.terminal.error("Error: " + msg);
                    window.terminal.echo((performance.now() - startTime).toFixed(2) + "ms");
                    window.terminal.resume();
                },
                function () {
                    if (!printedResult) {
                        window.terminal.echo("Done. " + (performance.now() - startTime).toFixed(2) + "ms");
                    }
                    window.terminal.resume();
                });
        };

        self.shellFn.js = function (command) {
            // Evaluate commands of the form "<method>(<arg*>)".
            var i = command.indexOf("(");
            if (i < 0) {
                i = command.length;
                command += "()";
            }

            var method = command.substr(0, i);
            var args = command.substr(i + 1).replace(/\){1};{0,1}$/, "");
            if (args) {
                args = ",[" + args + "]";
            }

            var result = window.eval("self.reqJs(self.req(\"" + method + "\"" + args + "));");
            if (result != undefined) {
                if (typeof result === "object") {
                    window.terminal.echo(JSON.stringify(result, null, "  "));
                } else {
                    window.terminal.echo(String(result));
                }
            }
        };

        self.complete = function (terminal, commandIgnored, callback) {
            var command = terminal.get_command();
            var cmd = $.terminal.parse_command(command);
            if (cmd.name === "to") {
                if (cmd.args.length === 1) {
                    var arg = cmd.args[0];
                    if (arg[arg.length - 1] === "/") {
                        callback(["to " + arg + emptyGuid]);
                        return;
                    }
                }

                $.get(self.completionEndpoint + "type/" + (cmd.args[0] || "")).then(function (val) {
                    var results = [];
                    for (var i = 0; i < val.length; ++i) {
                        results.push(val[i]);
                    }

                    callback(results);
                });
            } else {
                var type = self.to.substr(0, self.to.indexOf("/"));
                var partial = { type: type, cmd: cmd.name, args: cmd.args };
                $.ajax({
                    method: "POST",
                    url: self.completionEndpoint + "command",
                    data: JSON.stringify(partial),
                    contentType: "application/json"
                }).then(function (val) {
                    for (var key in self.commands) {
                        if (self.commands.hasOwnProperty(key) && key !== "to") {
                            val.push(key);
                        }
                    }
                    callback(val);
                });
            }
        };

        self.commands = [];

        self.getHandler = function (name) {
            var c = self.commands[name];
            if (typeof c === "string") {
                return self.getHandler(c);
            } else if (c) {
                return c.fn;
            }

            return null;
        };

        self.handleCommand = function (command, terminal) {
            command = command.trim();
            if (command.length === 0) {
                return;
            }

            window.terminal = terminal;
            var cmd = $.terminal.parse_command(command);
            var handler = getHandler(cmd.name);
            if (handler) {
                handler(cmd);
            } else if (command.match(/^[$A-Z_][0-9A-Z_$]*($|\s)/i)) {
                // Let users skip the 'req' bit if an unknown command looks like a request.
                self.shellFn.req($.terminal.parse_command("req " + command));
            } else {
                // Interpret all other commands as JavaScript.
                window.terminal.error("Unknown command. Try 'js' for a JavaScript terminal, or 'help' for a list of commands.");
            }
        };

        self.usage = function () {
            var result = "Usage:";
            for (var i in self.commands) {
                if (commands.hasOwnProperty(i)) {
                    var cmd = commands[i];

                    var u;
                    var h = "";
                    if (typeof cmd === "string") {
                        u = "alias for " + cmd + ".";
                    } else {
                        u = cmd.usage;
                        if (cmd.help) {
                            h = " - " + cmd.help;
                        }
                    }

                    result += "\n\t" + i + " - " + u + h;
                }
            }

            result += "\n** Use tab completion. **";

            return result;
        };

        var systemMethods = {};
        (function () {
            $.ajax({
                method: "GET",
                url: self.baseUrl + "complete/grains",
                contentType: "application/json"
            }).then(function (result) { systemMethods = result });
        })();
        self.help = function () {
            if (self.to.indexOf("/") < 0) {
                return;
            }

            var type = self.to.substr(0, self.to.indexOf("/"));
            if (type !== "") {
                var methods = systemMethods[type].methods;
                if (methods) {
                    for (var method in methods) {
                        if (methods.hasOwnProperty(method)) {

                            // Get arguments.
                            var args = "";
                            for (var arg = 0; arg < methods[method].args.length; arg++) {
                                var methodArg = methods[method].args[arg];
                                if (args) {
                                    args += ", ";
                                }

                                args += methodArg.type + " " + methodArg.name;
                            }

                            // Get method help.
                            window.terminal.echo((methods[method].returnType || "void") + " " + methods[method].name + "(" + args + ")");
                        }
                    }
                }
            }
        }

        self.stateStack = [];
        self.commands = {
            guid: {
                usage: "guid",
                help: "returns a new guid",
                fn: function () {
                    window.terminal.echo(guid());
                }
            },
            req: {
                usage: "req <event type> [<event args>] [<from address>]",
                help: "sends a request.",
                fn: self.shellFn.req
            },
            usage: {
                usage: "usage",
                fn: function () {
                    window.terminal.echo(self.usage());
                }
            },
            help: {
                usage: "help",
                fn: self.help
            },
            js: {
                usage: "js [cmd]",
                help: "enter a JavaScript command prompt if no argument is provided. If arguments are provided, evaluates the arguments.",
                fn: function (cmd) {
                    if (!cmd.args || cmd.args.length === 0) {
                        // Enter JS console.
                        var name = self.to + " js";
                        window.terminal.push(
                            self.shellFn.js,
                            {
                                prompt: name + "> ",
                                name: name
                            });
                    } else {
                        // Eval one command.
                        self.shellFn.js(cmd.args.join(" "));
                    }
                }
            },
            to: {
                usage: "to <address>",
                help: "set the new command target, eg some item (to item/<guid>), some chat room, sanic (to sanic), etc.",
                fn: function (cmd) {
                    var addr = cmd.args[0];
                    if (addr.indexOf("/") <= 0) {
                        addr += "/";
                    }

                    if (addr[addr.length - 1] === "/") {
                        addr += emptyGuid;
                    }

                    window.terminal.push(
                        handleCommand,
                        {
                            prompt: addr + "> ",
                            name: addr,
                            onStart: function () {
                                self.stateStack.push(self.to);
                                self.to = addr;
                            },
                            onExit: function () {
                                self.to = self.stateStack.pop();
                            },
                            completion: self.complete
                        });
                }
            }
        };

        return self;
    }

   /* if (!token) {
        navigateToLoginPage();
    }*/

    var shell = admin();
    window.terminal = $("#terminal").terminal(shell.handleCommand, {
        prompt: shell.to + "> ",
        name: shell.to,
        completion: shell.complete,
        greetings:
"   ____       _                      \n" +
"  / __ \\     | |                       Type 'help' for some available commands.\n" +
" | |  | |_ __| | ___  __ _ _ __  ___   Use tab-completion for assistance.\n" +
" | |  | | '__| |/ _ \\/ _` | '_ \\/ __|\n" +
" | |__| | |  | |  __/ (_| | | | \\__ \\\n" +
"  \\____/|_|  |_|\\___|\\__,_|_| |_|___/\n" +
"                                     \n",
        onBlur: function () {
            // prevent loosing focus
            return false;
        }
    });
});