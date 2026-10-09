# Rasterises SVG drawings to PNGs for the art scripts, which only have Python 2.7 and PIL - and
# PIL cannot draw a curve. Godot can (ThorVG), so a script hands it a list of jobs:
#
#     "$GODOT_BIN" --headless --script tools/art/svg_raster.gd -- jobs.json
#
# jobs.json is a list of [svg path, png path]. Each SVG's own width and height set its size in
# pixels. Godot's SVG renderer has no fonts, so text in an SVG does not come out.
extends SceneTree

func _init():
	var jobs = JSON.parse_string(FileAccess.get_file_as_string(OS.get_cmdline_user_args()[0]))
	for job in jobs:
		var image = Image.new()
		if image.load_svg_from_string(FileAccess.get_file_as_string(job[0]), 1.0) != OK:
			printerr("svg_raster: could not read ", job[0])
			continue
		image.save_png(job[1])
	quit()
