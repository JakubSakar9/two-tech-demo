#include <footprint_painter.hpp>

namespace godot
{

void FootprintPainter::_ready()
{
    device = RenderingServer::get_singleton()->get_rendering_device();
    RenderingServer::get_singleton()->call_on_render_thread(Callable(this, "init_compute"));
}

void FootprintPainter::_process(double delta)
{
    RenderingServer::get_singleton()->call_on_render_thread(Callable(this, "dispatch_compute"));
}

void FootprintPainter::_exit_tree()
{
    free_if_valid(shader, "shader");
    free_if_valid(pipeline, "pipeline");
    free_if_valid(compute_texture, "compute_texture");
    free_if_valid(footprint_texture_rid, "footprint_texture_rid");
    free_if_valid(footprint_sampler, "footprint_sampler");
    free_if_valid(uniform_set, "uniform_set");
}

void FootprintPainter::update_feet(Vector2 p_c_left, Vector2 p_c_right, float p_d_left, float p_d_right)
{
    params.center_left = p_c_left;
    params.center_right = p_c_right;
    params.depth_left = p_d_left;
    params.depth_right = p_d_right;
}

void FootprintPainter::set_angle(float p_angle)
{
    params.rotation_mat = Vector4(cosf(p_angle), -sinf(p_angle), sinf(p_angle), cosf(p_angle));
}

FootprintPainter::FootprintPainter()
{
    // footprint_texture =  Ref<Texture2D>(memnew(Texture2D));
    displacement_texture = Ref<Texture2DRD>(memnew(Texture2DRD));
    params = {
        .center_left = Vector2(0.5f, 0.5f),
        .center_right = Vector2(0.5f, 0.5f),
        .depth_left = 0.0f,
        .depth_right = 0.0f,
        .texture_size = texture_size,
        .downscale_factor = 1.6f
    };
    set_angle(0.0f);
}

FootprintPainter::~FootprintPainter()
{
}

void FootprintPainter::_bind_methods()
{
    ClassDB::bind_method(D_METHOD("update_feet"), &FootprintPainter::update_feet);
    ClassDB::bind_method(D_METHOD("set_angle"), &FootprintPainter::set_angle);
    ClassDB::bind_method(D_METHOD("init_compute"), &FootprintPainter::init_compute);
    ClassDB::bind_method(D_METHOD("dispatch_compute"), &FootprintPainter::dispatch_compute);

    ClassDB::bind_method(D_METHOD("set_texture_size"), &FootprintPainter::set_texture_size);
    ClassDB::bind_method(D_METHOD("set_footprint_texture"), &FootprintPainter::set_footprint_texture);
    ClassDB::bind_method(D_METHOD("get_texture_size"), &FootprintPainter::get_texture_size);
    ClassDB::bind_method(D_METHOD("get_footprint_texture"), &FootprintPainter::get_footprint_texture);

    ADD_PROPERTY(PropertyInfo(Variant::INT, "texture_size"), "set_texture_size", "get_texture_size");
    ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "footprint_texture", PROPERTY_HINT_RESOURCE_TYPE, "Texture2D"), "set_footprint_texture", "get_footprint_texture");
}

void FootprintPainter::init_compute()
{
    if (!footprint_texture.is_valid())
    {
        WARN_PRINT("Footprint texture is undefined or invalid, the footprint painter will do nothing.");
        return;
    }
    init_shader();
    init_target_texture();
    init_footprint_texture();
    init_uniforms();
    pipeline = device->compute_pipeline_create(shader);
}

void FootprintPainter::init_shader()
{
    Ref<RDShaderFile> shader_file = ResourceLoader::get_singleton()->load("res://shaders/disp_compute.glsl", "RDShaderFile");
    auto shader_bytecode = shader_file->get_spirv();
    shader = device->shader_create_from_spirv(shader_bytecode);

}

void FootprintPainter::init_target_texture()
{
    format = Ref<RDTextureFormat>(memnew(RDTextureFormat));
    format->set_width(texture_size);
    format->set_height(texture_size);
    format->set_format(RenderingDevice::DATA_FORMAT_R32_SFLOAT);
    typedef RenderingDevice::TextureUsageBits UBits;
    format->set_usage_bits(UBits::TEXTURE_USAGE_CAN_UPDATE_BIT
        | UBits::TEXTURE_USAGE_STORAGE_BIT
        | UBits::TEXTURE_USAGE_CPU_READ_BIT
        | UBits::TEXTURE_USAGE_CAN_COPY_FROM_BIT
        | UBits::TEXTURE_USAGE_SAMPLING_BIT);
    Ref<Image> compute_im = Image::create_empty(texture_size, texture_size, false, Image::FORMAT_RF);
    TypedArray<PackedByteArray> raw_data { compute_im->get_data() };
    view = Ref<RDTextureView>(memnew(RDTextureView));
    compute_texture = device->texture_create(format, view, raw_data);
    displacement_texture->set_texture_rd_rid(compute_texture);
}

void FootprintPainter::init_footprint_texture()
{
    int32_t ft_size = footprint_texture->get_width();
    Ref<RDTextureFormat> ft_format(memnew(RDTextureFormat));
    ft_format->set_width(ft_size);
    ft_format->set_height(ft_size);
    ft_format->set_format(RenderingDevice::DATA_FORMAT_R8_UNORM);
    ft_format->set_usage_bits(RenderingDevice::TEXTURE_USAGE_SAMPLING_BIT);
    ft_format->set_mipmaps(8);
    Ref<RDTextureView> ft_view(memnew(RDTextureView));
    Ref<Image> ft_im = footprint_texture->get_image();
    TypedArray<PackedByteArray> raw_data { ft_im->get_data() };
    footprint_texture_rid = device->texture_create(ft_format, ft_view, raw_data);

    Ref<RDSamplerState> sampler_state(memnew(RDSamplerState));
    sampler_state->set_min_filter(RenderingDevice::SAMPLER_FILTER_LINEAR);
    sampler_state->set_mag_filter(RenderingDevice::SAMPLER_FILTER_LINEAR);
    sampler_state->set_repeat_u(RenderingDevice::SAMPLER_REPEAT_MODE_CLAMP_TO_BORDER);
    sampler_state->set_repeat_v(RenderingDevice::SAMPLER_REPEAT_MODE_CLAMP_TO_BORDER);
    footprint_sampler = device->sampler_create(sampler_state);
    if (!footprint_sampler.is_valid())
    {
        WARN_PRINT("Sampler is not valid");
    }
}

void FootprintPainter::init_uniforms()
{
    Ref<RDUniform> compute_tex_uniform(memnew(RDUniform));
    compute_tex_uniform->set_uniform_type(RenderingDevice::UNIFORM_TYPE_IMAGE);
    compute_tex_uniform->set_binding(0);
    compute_tex_uniform->add_id(compute_texture);

    Ref<RDUniform> footprint_tex_uniform(memnew(RDUniform));
    footprint_tex_uniform->set_uniform_type(RenderingDevice::UNIFORM_TYPE_SAMPLER_WITH_TEXTURE);
    footprint_tex_uniform->set_binding(1);
    footprint_tex_uniform->add_id(footprint_sampler);
    footprint_tex_uniform->add_id(footprint_texture_rid);

    uniforms.append(compute_tex_uniform);
    uniforms.append(footprint_tex_uniform);
    uniform_set = device->uniform_set_create(uniforms, shader, 0);
}

void FootprintPainter::dispatch_compute()
{
    if (!footprint_texture.is_valid()) return;

    uint32_t x_groups = texture_size / 16;
    uint32_t y_groups = texture_size / 16;
    uint32_t z_groups = 1;

    auto compute_list = device->compute_list_begin();
    device->compute_list_bind_compute_pipeline(compute_list, pipeline);
    device->compute_list_bind_uniform_set(compute_list, uniform_set, 0);

    PackedByteArray param_data;
    param_data.resize(sizeof(TPParams));
    uint8_t* write = param_data.ptrw();
    std::memcpy(write, &params, sizeof(TPParams));
    device->compute_list_set_push_constant(compute_list, param_data, sizeof(TPParams));

    device->compute_list_dispatch(compute_list, x_groups, y_groups, z_groups);
    device->compute_list_end();
}

void FootprintPainter::set_texture_size(const int32_t p_size)
{
    texture_size = p_size;
    params.texture_size = p_size;
}

void FootprintPainter::set_footprint_texture(const Ref<Texture2D> &p_texture)
{
    bool was_valid = footprint_texture.is_valid();
    footprint_texture = p_texture;
    if (!was_valid) init_compute();
}

int32_t FootprintPainter::get_texture_size()
{
    return texture_size;
}

Ref<Texture2D> FootprintPainter::get_footprint_texture()
{
    return footprint_texture;
}

} // namespace godot
