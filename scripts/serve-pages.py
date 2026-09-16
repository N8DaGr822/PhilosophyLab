"""Local Pages-like host: serves the custom 404 body with status 404, never an SPA 200 fallback."""
import argparse
import http.server
import pathlib
import urllib.parse

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=pathlib.Path)
parser.add_argument("--base-path", default="/PhilosophyLab/")
parser.add_argument("--port", type=int, default=5158)
args = parser.parse_args()
root = args.directory.resolve()


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(root), **kw)

    def do_GET(self):
        path = urllib.parse.urlsplit(self.path).path
        if not path.startswith(args.base_path):
            self.send_error(404)
            return
        self.path = "/" + self.path[len(args.base_path):]
        super().do_GET()

    def send_error(self, code, message=None, explain=None):
        if code == 404 and (root / "404.html").exists():
            body = (root / "404.html").read_bytes()
            self.send_response(404)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        else:
            super().send_error(code, message, explain)


print(f"Serving http://127.0.0.1:{args.port}{args.base_path}", flush=True)
http.server.ThreadingHTTPServer(("127.0.0.1", args.port), Handler).serve_forever()
